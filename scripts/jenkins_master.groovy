/*
*  BepInEx Bleeding Edge build CI jenkinsfile
*/
pipeline {
    agent any
    parameters {
        // Check if the build is Bleeding Edge. Affects whether the result is pushed to BepisBuilds
        booleanParam(name: "IS_BE")
    }
    stages {
        stage('Pull Projects') {
            steps {
                script {
                    if(fileExists('./last_build_commit'))
                        lastBuildCommit = readFile 'last_build_commit'
                    else 
                        lastBuildCommit = ""
                }
                // Clean up old project before starting
                cleanWs()

                dir('BepInEx') {
                    git 'https://github.com/BepInEx/BepInEx.git'
                    
                    script {
                        shortCommit = sh(returnStdout: true, script: "git log -n 1 --pretty=format:'%h'").trim()
                        longCommit = sh(returnStdout: true, script: "git rev-parse HEAD").trim()
                        branchName = sh(returnStdout: true, script: "git rev-parse --abbrev-ref HEAD").trim()
                        latestTag = sh(returnStdout: true, script: "git describe --tags `git rev-list --tags --max-count=1`").trim()

                        if(!params.IS_BE) {
                            latestTagOnCurrentBranch = sh(returnStdout: true, script: "git describe --abbrev=0 --tags").trim()
                            sh("git checkout ${latestTagOnCurrentBranch}")
                        }

                        if(params.IS_BE)
                            changelog = gitChangelog from: [type: 'REF', value: "${latestTag}"], returnType: 'STRING', template: """BepInEx Bleeding Edge Changelog
Changes since ${latestTag}:

{{#commits}}
{{^merge}}
* [{{hash}}] ({{authorName}}) {{messageTitle}}
{{/merge}}
{{/commits}}""", to: [type: 'COMMIT', value: "${longCommit}"]

                        if(lastBuildCommit != "") {
                            htmlChangelog = gitChangelog from: [type: 'COMMIT', value: lastBuildCommit], returnType: 'STRING',
                            template: """<ul>{{#commits}}{{^merge}}<li>[<code>{{hash}}</code>] ({{authorName}}) {{messageTitle}}</li>{{/merge}}{{/commits}}</ul>""", to: [type: 'COMMIT', value: "${longCommit}"] }
                        else
                            htmlChangelog = ""
                    }

                    sh 'git submodule update --init --recursive'
                }

                dir('Unity') {
                     withCredentials([string(credentialsId: 'bepis_dll_git_url', variable: 'bepis_dll_git_url')]) {
                        git credentialsId: 'b1f2f78b-f0c5-4a81-8b4a-55b6b8bdbbe3', url: "${bepis_dll_git_url}/JenkinsStuff/UnityDLL.git"
                     }
                }

                dir('Doorstop') {
                    sh '''  tag="v2.11.1.0";
                    version="2.11.1.0";
                    wget https://github.com/NeighTools/UnityDoorstop/releases/download/$tag/Doorstop_x64_$version.zip;
                    wget https://github.com/NeighTools/UnityDoorstop/releases/download/$tag/Doorstop_x86_$version.zip;
                    unzip -o Doorstop_x86_$version.zip winhttp.dll -d x86;
                    unzip -o Doorstop_x64_$version.zip winhttp.dll -d x64;'''
                }
            }
        }
        stage('Prepare BepInEx') {
            steps {
                dir('BepInEx') {
                    sh "mkdir -p lib"

                    // Ghetto fix to force TargetFrameworks to only net35
                    sh "find . -type f -name \"*.csproj\" -exec sed -i -E \"s/(<TargetFrameworks>)[^<]+(<\\/TargetFrameworks>)/\\1net35\\2/g\" {} +"
                    sh "nuget restore"
                }

                dir('BepInEx/BepInEx') {
                    // Write additional BuildInfo into the Bleeding Edge BepInEx.dll
                    dir('Properties') {
                        script {
                            if(params.IS_BE) {
                                sh "sed -i -E \"s/([0-9]+\\.[0-9]+\\.[0-9]+\\.)[0-9]+/\\1${env.BUILD_NUMBER}/\" AssemblyInfo.cs"
                                sh "echo '[assembly: BuildInfo(\"BLEEDING EDGE Build #${env.BUILD_NUMBER} from ${shortCommit} at ${branchName}\")]' >> AssemblyInfo.cs"
                            }
                            versionNumber = sh(returnStdout: true, script: "grep -m 1 -oE \"[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+\" AssemblyInfo.cs").trim()
                        }
                    }
                }
            }
        }
        stage('Build BepInEx') {
            steps {
                sh 'cp -f Unity/5.6/UnityEngine.dll BepInEx/lib/UnityEngine.dll'
                dir('BepInEx') {
                    sh 'msbuild /p:Configuration=Release /t:Build /p:DebugType=none BepInEx.sln'
                }

                dir('Build/bin') {
                    sh 'cp -fr ../../BepInEx/bin/* .'
                }
                dir('BepInEx/bin') {
                    deleteDir()
                }
            }
        }
        stage('Package') {
            steps {
                dir('Build/dist') {
                    sh 'mkdir -p BepInEx/core BepInEx/patchers BepInEx/plugins'
                    sh 'cp -fr -t BepInEx/core ../bin/*'
                    sh 'rm -f BepInEx/core/UnityEngine.dll'
                    sh 'rm -rf BepInEx/core/patcher'

                    sh 'cp -f ../../BepInEx/doorstop/doorstop_config.ini doorstop_config.ini'
                    sh 'cp -f ../../Doorstop/x86/winhttp.dll winhttp.dll'
                    
                    script {
                        if(params.IS_BE) {
                            writeFile encoding: 'UTF-8', file: 'changelog.txt', text: changelog
                            sh 'unix2dos changelog.txt'
                            commitPrefix = "_${shortCommit}_"
                        } 
                        else
                            commitPrefix = "_"
                    }

                    sh "zip -r9 BepInEx_x86${commitPrefix}${versionNumber}.zip ./*"
                    
                    sh 'cp -f ../../Doorstop/x64/winhttp.dll winhttp.dll'
                    
                    sh 'unix2dos doorstop_config.ini'
                    
                    sh "zip -r9 BepInEx_x64${commitPrefix}${versionNumber}.zip ./* -x \\*.zip"

                    archiveArtifacts "*.zip"
                }
                dir('Build/patcher') {
                    sh 'cp -fr ../bin/patcher/* .'
                    script {
                        if(params.IS_BE) {
                            writeFile encoding: 'UTF-8', file: 'changelog.txt', text: changelog
                            sh 'unix2dos changelog.txt'
                        }
                    }
                    sh "zip -r9 BepInEx_Patcher${commitPrefix}${versionNumber}.zip ./*"

                    archiveArtifacts "*.zip"
                }

                dir('Artifacts') {
                    sh 'mv ../Build/patcher/*.zip .'
                    sh 'mv ../Build/dist/*.zip .'
                }
            }
        }
    }
    post {
        cleanup {
            script {
                writeFile file: 'last_build_commit', text: lastBuildCommit
            }
        }
        success {
            script {
                lastBuildCommit = longCommit
                if(params.IS_BE) {
                    dir('Artifacts') {
                        def data = [
                            id: env.BUILD_NUMBER,
                            date: sh(returnStdout: true, script: 'date -Iseconds -u'),
                            changelog: htmlChangelog,
                            artifacts: [
                                [
                                    file: "BepInEx_x64${commitPrefix}${versionNumber}.zip",
                                    description: "BepInEx for x64 machines"
                                ],
                                [
                                    file: "BepInEx_x86${commitPrefix}${versionNumber}.zip",
                                    description: "BepInEx for x86 machines"
                                ],
                                [
                                    file: "BepInEx_Patcher${commitPrefix}${versionNumber}.zip",
                                    description: "Hardpatcher for BepInEx. IMPORTANT: USE ONLY IF DOORSTOP DOES NOT WORK FOR SOME REASON!"
                                ]
                            ]
                        ]

                        def json = groovy.json.JsonOutput.toJson(data)
                        json = groovy.json.JsonOutput.prettyPrint(json)
                        writeFile file: 'info.json', text: json

                        def filesToSend = findFiles(glob: '*').collect {it.name}
                        withCredentials([string(credentialsId: 'bepisbuilds_addr', variable: 'BEPISBUILDS_ADDR')]) {
                            sh """curl --upload-file "{${filesToSend.join(',')}}" --ftp-pasv --ftp-skip-pasv-ip --ftp-create-dirs --ftp-method singlecwd --disable-epsv ftp://${BEPISBUILDS_ADDR}/bepinex_be/artifacts/${env.BUILD_NUMBER}/"""
                        }
                    }
                }
            }

            //Notify Bepin Discord of successfull build
            withCredentials([string(credentialsId: 'discord-notify-webhook', variable: 'DISCORD_WEBHOOK')]) {
                discordSend description: "**Build:** [${currentBuild.id}](${env.BUILD_URL})\n**Status:** [${currentBuild.currentResult}](${env.BUILD_URL})\n\n[**Artifacts on BepisBuilds**](https://builds.bepis.io/projects/bepinex_be)", footer: 'Jenkins via Discord Notifier', link: env.BUILD_URL, successful: currentBuild.resultIsBetterOrEqualTo('SUCCESS'), title: "${env.JOB_NAME} #${currentBuild.id}", webhookURL: DISCORD_WEBHOOK
            }
        }
        failure {
            //Notify Discord of failed build
            withCredentials([string(credentialsId: 'discord-notify-webhook', variable: 'DISCORD_WEBHOOK')]) {
                discordSend description: "**Build:** [${currentBuild.id}](${env.BUILD_URL})\n**Status:** [${currentBuild.currentResult}](${env.BUILD_URL})", footer: 'Jenkins via Discord Notifier', link: env.BUILD_URL, successful: currentBuild.resultIsBetterOrEqualTo('SUCCESS'), title: "${env.JOB_NAME} #${currentBuild.id}", webhookURL: DISCORD_WEBHOOK
            }
        }
    }
}