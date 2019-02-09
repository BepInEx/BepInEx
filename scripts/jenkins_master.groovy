/*
*  BepInEx Bleeding Edge build CI jenkinsfile
*/
pipeline {
    agent any
    stages {
        stage('Pull Projects') {
            steps {
                // Clean up old project before starting
                cleanWs()

                dir('BepInEx') {
                    git 'https://github.com/BepInEx/BepInEx.git'
                    sh 'git submodule update --init --recursive'
                    script {
                        shortCommit = sh(returnStdout: true, script: "git log -n 1 --pretty=format:'%h'").trim()
                        longCommit = sh(returnStdout: true, script: "git rev-parse HEAD").trim()
                        branchName = sh(returnStdout: true, script: "git rev-parse --abbrev-ref HEAD").trim()
                        latestTag = sh(returnStdout: true, script: "git describe --tags `git rev-list --tags --max-count=1`").trim()
                        changelog = gitChangelog from: [type: 'REF', value: "${latestTag}"], returnType: 'STRING', template: """BepInEx Bleeding Edge Changelog
Changes since ${latestTag}:

{{#commits}}
{{^merge}}
* [{{hash}}] ({{authorName}}) {{messageTitle}}
{{/merge}}
{{/commits}}""", to: [type: 'COMMIT', value: "${longCommit}"]
                    }
                }

                dir('Unity') {
                    git credentialsId: 'b1f2f78b-f0c5-4a81-8b4a-55b6b8bdbbe3', url: 'http://localhost:6000/JenkinsStuff/UnityDLL.git'
                }

                dir('Doorstop') {
                    sh '''  tag="$(curl -s https://api.github.com/repos/NeighTools/UnityDoorstop/releases/latest | grep "tag_name" | cut -d : -f 2 | tr -d "\\", ")";
                    version="$(echo $tag | cut -c 2-)";
                    wget https://github.com/NeighTools/UnityDoorstop/releases/download/$tag/Doorstop_x64_$version.zip;
                    wget https://github.com/NeighTools/UnityDoorstop/releases/download/$tag/Doorstop_x86_$version.zip;
                    unzip -o Doorstop_x86_$version.zip winhttp.dll -d x86;
                    unzip -o Doorstop_x64_$version.zip winhttp.dll -d x64;'''
                }
            }
        }
        stage('Build BepInEx') {
            steps {
                //TODO: Add builds for other Unity versions as well
                sh 'cp -f Unity/5.6/UnityEngine.dll BepInEx/lib/UnityEngine.dll'
                dir('BepInEx') {
                    sh 'nuget restore'
                }
                dir('BepInEx/BepInEx') {

                    // Write additional BuildInfo into the Bleeding Edge BepInEx.dll
                    dir('Properties') {
                        sh "echo '[assembly: BuildInfo(\"BLEEDING EDGE Build #${env.BUILD_NUMBER} from ${shortCommit} at ${branchName}\")]' >> AssemblyInfo.cs"
                        sh "sed -i -E \"s/([0-9]+\\.[0-9]+\\.[0-9]+\\.)[0-9]+/\\1${env.BUILD_NUMBER}/\" AssemblyInfo.cs"
                        script {
                            versionNumber = sh(returnStdout: true, script: "grep -m 1 -oE \"[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+\" AssemblyInfo.cs").trim()
                        }
                    }
                }
                // TODO: Build stuff selectively?
                dir('BepInEx') {
                    sh 'msbuild /p:Configuration=Legacy /t:Build /p:DebugType=none BepInEx.sln'
                }
            }
        }
        stage('Package everything') {
            steps {
                dir('Build') {
                    deleteDir()
                }
                dir('BepInEx/bin/patcher') {
                    // NOTE: Currently we exclude patcher because we don't need it!
                    deleteDir()
                }
                dir('Build') {
                    sh 'mkdir -p BepInEx/core BepInEx/patchers'
                    sh 'cp -f -t BepInEx/core ../BepInEx/bin/*'
                    sh 'rm -f BepInEx/core/UnityEngine.dll'

                    sh 'cp -f ../BepInEx/doorstop/doorstop_config.ini doorstop_config.ini'
                    sh 'cp -f ../Doorstop/x86/winhttp.dll winhttp.dll'
                    
                    writeFile encoding: 'UTF-8', file: 'changelog.txt', text: changelog

                    sh "zip -r9 BepInEx_x86_${shortCommit}_${versionNumber}.zip ./*"
                    
                    sh 'cp -f ../Doorstop/x64/winhttp.dll winhttp.dll'
                    
                    sh 'unix2dos doorstop_config.ini changelog.txt'
                    
                    sh "zip -r9 BepInEx_x64_${shortCommit}_${versionNumber}.zip ./* -x \\*.zip"
                }
            }
        }
        stage('Saving artifacts') {
            steps {
                archiveArtifacts 'Build/*.zip'
            }
        }
    }
    post {
        success {
            // Write built BepInEx into bepisbuilds
            dir('Build') {
                sh "cp BepInEx_x86_${shortCommit}_${versionNumber}.zip /var/www/bepisbuilds/builds/bepinex_be"
                sh "cp BepInEx_x64_${shortCommit}_${versionNumber}.zip /var/www/bepisbuilds/builds/bepinex_be"
                sh "echo \"`date -Iseconds -u`;${env.BUILD_NUMBER};${shortCommit};BepInEx_x86_${shortCommit}_${versionNumber}.zip;BepInEx_x64_${shortCommit}_${versionNumber}.zip\" >> /var/www/bepisbuilds/builds/bepinex_be/artifacts_list"
            }

            // Notify Bepin Discord of successfull build
            withCredentials([string(credentialsId: 'discord-notify-webhook', variable: 'DISCORD_WEBHOOK')]) {
                discordSend description: "**Build:** [${currentBuild.id}](${env.BUILD_URL})\n**Status:** [${currentBuild.currentResult}](${env.BUILD_URL})\n\n**Artifacts:**\n- [BepInEx_x86_${shortCommit}_${versionNumber}.zip](${env.BUILD_URL}artifact/Build/BepInEx_x86_${shortCommit}_${versionNumber}.zip)\n- [BepInEx_x64_${shortCommit}_${versionNumber}.zip](${env.BUILD_URL}artifact/Build/BepInEx_x64_${shortCommit}_${versionNumber}.zip)", footer: 'Jenkins via Discord Notifier', link: env.BUILD_URL, successful: currentBuild.resultIsBetterOrEqualTo('SUCCESS'), title: "${env.JOB_NAME} #${currentBuild.id}", webhookURL: DISCORD_WEBHOOK
            }
        }
        failure {
            // Notify Discord of failed build
            withCredentials([string(credentialsId: 'discord-notify-webhook', variable: 'DISCORD_WEBHOOK')]) {
                discordSend description: "**Build:** [${currentBuild.id}](${env.BUILD_URL})\n**Status:** [${currentBuild.currentResult}](${env.BUILD_URL})", footer: 'Jenkins via Discord Notifier', link: env.BUILD_URL, successful: currentBuild.resultIsBetterOrEqualTo('SUCCESS'), title: "${env.JOB_NAME} #${currentBuild.id}", webhookURL: DISCORD_WEBHOOK
            }
        }
    }
}