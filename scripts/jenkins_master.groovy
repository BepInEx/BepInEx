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
                    }

                    sh 'git submodule update --init --recursive'
                }

                dir('Unity') {
                    git credentialsId: 'b1f2f78b-f0c5-4a81-8b4a-55b6b8bdbbe3', url: 'http://localhost:6000/JenkinsStuff/UnityDLL.git'
                }

                dir('Doorstop') {
                    sh '''  tag="v2.9.0.0";
                    version="2.9.0.0";
                    wget https://github.com/NeighTools/UnityDoorstop/releases/download/$tag/Doorstop_x64_$version.zip;
                    wget https://github.com/NeighTools/UnityDoorstop/releases/download/$tag/Doorstop_x86_$version.zip;
                    unzip -o Doorstop_x86_$version.zip version.dll -d x86;
                    unzip -o Doorstop_x64_$version.zip version.dll -d x64;'''
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
        stage('Build Legacy BepInEx') {
            steps {
                sh 'cp -f Unity/5.6/UnityEngine.dll BepInEx/lib/UnityEngine.dll'
                dir('BepInEx') {
                    sh 'msbuild /p:Configuration=Legacy /t:Build /p:DebugType=none BepInEx.sln'
                }

                dir('Build/Legacy/bin') {
                    sh 'cp -fr ../../../BepInEx/bin/* .'
                }
                dir('BepInEx/bin') {
                    deleteDir()
                }
            }
        }
        stage('Build 2018 BepInEx') {
            steps {
                // sh 'cp -f Unity/2018/UnityEngine.CoreModule.dll BepInEx/lib/UnityEngine.CoreModule.dll'
                // TODO: Switch to 2018 version of UnityEngine.dll?
                sh 'cp -f Unity/5.6/UnityEngine.dll BepInEx/lib/UnityEngine.dll'
                dir('BepInEx') {
                    sh 'msbuild /p:Configuration=v2018 /t:Build /p:DebugType=none BepInEx.sln'
                }

                dir('Build/v2018/bin') {
                    sh 'cp -fr ../../../BepInEx/bin/* .'
                }
                dir('BepInEx/bin') {
                    deleteDir()
                }
            }
        }
        stage('Package Legacy') {
            steps {
                dir('Build/Legacy/dist') {
                    sh 'mkdir -p BepInEx/core BepInEx/patchers BepInEx/plugins'
                    sh 'cp -fr -t BepInEx/core ../bin/*'
                    sh 'rm -f BepInEx/core/UnityEngine.dll'
                    sh 'rm -rf BepInEx/core/patcher'

                    sh 'cp -f ../../../BepInEx/doorstop/doorstop_config.ini doorstop_config.ini'
                    sh 'cp -f ../../../Doorstop/x86/version.dll version.dll'
                    
                    script {
                        if(params.IS_BE) {
                            writeFile encoding: 'UTF-8', file: 'changelog.txt', text: changelog
                            sh 'unix2dos changelog.txt'
                            commitPrefix = "_${shortCommit}_"
                        } 
                        else
                            commitPrefix = "_"
                    }

                    sh "zip -r9 BepInEx_Legacy_x86${commitPrefix}${versionNumber}.zip ./*"
                    
                    sh 'cp -f ../../../Doorstop/x64/version.dll version.dll'
                    
                    sh 'unix2dos doorstop_config.ini'
                    
                    
                    sh "zip -r9 BepInEx_Legacy_x64${commitPrefix}${versionNumber}.zip ./* -x \\*.zip"

                    archiveArtifacts "*.zip"
                }
                dir('Build/Legacy/patcher') {
                    sh 'cp -fr ../bin/patcher/* .'
                    script {
                        if(params.IS_BE) {
                            writeFile encoding: 'UTF-8', file: 'changelog.txt', text: changelog
                            sh 'unix2dos changelog.txt'
                        }
                    }
                    sh "zip -r9 BepInEx_Legacy_Patcher${commitPrefix}${versionNumber}.zip ./*"

                    archiveArtifacts "*.zip"
                }
            }
        }
        stage('Package v2018') {
            steps {
                dir('Build/v2018/dist') {
                    sh 'mkdir -p BepInEx/core BepInEx/patchers BepInEx/plugins'
                    sh 'cp -fr -t BepInEx/core ../bin/*'
                    sh 'rm -rf BepInEx/core/patcher'
                    sh 'rm -f BepInEx/core/UnityEngine.dll'
                    sh 'rm -f BepInEx/core/UnityEngine.CoreModule.dll'

                    sh 'cp -f ../../../BepInEx/doorstop/doorstop_config.ini doorstop_config.ini'
                    sh 'cp -f ../../../Doorstop/x86/version.dll version.dll'
                    
                    script {
                        if(params.IS_BE) {
                            writeFile encoding: 'UTF-8', file: 'changelog.txt', text: changelog
                            sh 'unix2dos changelog.txt'
                            commitPrefix = "_${shortCommit}_"
                        } 
                        else
                            commitPrefix = "_"
                    }

                    sh "zip -r9 BepInEx_v2018_x86${commitPrefix}${versionNumber}.zip ./*"
                    
                    sh 'cp -f ../../../Doorstop/x64/version.dll version.dll'
                    
                    sh 'unix2dos doorstop_config.ini'
                    
                    sh "zip -r9 BepInEx_v2018_x64${commitPrefix}${versionNumber}.zip ./* -x \\*.zip"

                    archiveArtifacts "*.zip"
                }
                dir('Build/v2018/patcher') {
                    sh 'cp -fr ../bin/patcher/* .'
                    script {
                        if(params.IS_BE) {
                            writeFile encoding: 'UTF-8', file: 'changelog.txt', text: changelog
                            sh 'unix2dos changelog.txt'
                        }
                    }
                    sh "zip -r9 BepInEx_v2018_Patcher${commitPrefix}${versionNumber}.zip ./*"

                    archiveArtifacts "*.zip"
                }
            }
        }
    }
    post {
        success {
            script {
                if(params.IS_BE) {
                    // Write built BepInEx into bepisbuilds
                    dir('Build/Legacy/dist') {
                        sh "cp BepInEx_Legacy_x86_${shortCommit}_${versionNumber}.zip /var/www/bepisbuilds/builds/bepinex_be"
                        sh "cp BepInEx_Legacy_x64_${shortCommit}_${versionNumber}.zip /var/www/bepisbuilds/builds/bepinex_be"
                    }

                    dir('Build/Legacy/patcher') {
                        sh "cp BepInEx_Legacy_Patcher_${shortCommit}_${versionNumber}.zip /var/www/bepisbuilds/builds/bepinex_be"
                    }

                    dir('Build/v2018/dist') {
                        sh "cp BepInEx_v2018_x86_${shortCommit}_${versionNumber}.zip /var/www/bepisbuilds/builds/bepinex_be"
                        sh "cp BepInEx_v2018_x64_${shortCommit}_${versionNumber}.zip /var/www/bepisbuilds/builds/bepinex_be"
                    }

                    dir('Build/v2018/patcher') {
                        sh "cp BepInEx_v2018_Patcher_${shortCommit}_${versionNumber}.zip /var/www/bepisbuilds/builds/bepinex_be"
                    }

                    sh "echo \"`date -Iseconds -u`;${env.BUILD_NUMBER};${shortCommit};BepInEx_Legacy_x86_${shortCommit}_${versionNumber}.zip;BepInEx_Legacy_x64_${shortCommit}_${versionNumber}.zip;BepInEx_v2018_x86_${shortCommit}_${versionNumber}.zip;BepInEx_v2018_x64_${shortCommit}_${versionNumber}.zip;BepInEx_Legacy_Patcher_${shortCommit}_${versionNumber}.zip;BepInEx_v2018_Patcher_${shortCommit}_${versionNumber}.zip\" >> /var/www/bepisbuilds/builds/bepinex_be/artifacts_list"
                }
            }

            //Notify Bepin Discord of successfull build
            withCredentials([string(credentialsId: 'discord-notify-webhook', variable: 'DISCORD_WEBHOOK')]) {
                discordSend description: "**Build:** [${currentBuild.id}](${env.BUILD_URL})\n**Status:** [${currentBuild.currentResult}](${env.BUILD_URL})\n\n[**Artifacts on BepisBuilds**](http://builds.bepis.io/bepinex_be)", footer: 'Jenkins via Discord Notifier', link: env.BUILD_URL, successful: currentBuild.resultIsBetterOrEqualTo('SUCCESS'), title: "${env.JOB_NAME} #${currentBuild.id}", webhookURL: DISCORD_WEBHOOK
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