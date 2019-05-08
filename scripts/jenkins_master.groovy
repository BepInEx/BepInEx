pipeline {
    agent any
    parameters {
        string(name: "commit", defaultValue: "")
    }
    stages {
        stage('Pull Projects') {
            steps {
                cleanWs()
                dir('BepInEx') {
                    git 'https://github.com/BepInEx/BepInEx.git'
					sh "git checkout v4-legacy"
                    script {
                        // Get latest tag on legacy branch and build it
						shortCommit = sh(returnStdout: true, script: "git log -n 1 --pretty=format:'%h'").trim()						
                    }
                }
                dir('Unity') {
                    git credentialsId: 'b1f2f78b-f0c5-4a81-8b4a-55b6b8bdbbe3', url: 'http://localhost:6000/JenkinsStuff/UnityDLL.git'
                }
                dir('Doorstop') { deleteDir() }
                dir('Doorstop') {
                    sh '''tag="v2.7.1.0";
                    version="2.7.1.0";
                    wget https://github.com/NeighTools/UnityDoorstop/releases/download/$tag/Doorstop_x64_$version.zip;
                    wget https://github.com/NeighTools/UnityDoorstop/releases/download/$tag/Doorstop_x86_$version.zip;
                    unzip -o Doorstop_x86_$version.zip winhttp.dll -d x86;
                    unzip -o Doorstop_x64_$version.zip winhttp.dll -d x64;'''
                }
            }
        }
        stage('Build BepInEx') {
            steps {
                //NOTE: Now building only against 5.6!
                sh 'cp -f Unity/5.6/UnityEngine.dll BepInEx/lib/UnityEngine.dll'
                dir('BepInEx') {
                    sh 'nuget restore'
                }
                dir('BepInEx/BepInEx') {
                    sh 'msbuild /p:Configuration=Release /t:Build /p:DebugType=none BepInEx.csproj'
                }
            }
        }
        stage('Package everything') {
            steps {
                dir('Build') {
                    deleteDir()
                }
                dir('Build') {
                    sh 'mkdir -p BepInEx/core BepInEx/patchers'
                    sh 'cp -f -t BepInEx/core ../BepInEx/bin/*'

                    sh 'cp -f ../BepInEx/doorstop/doorstop_config.ini doorstop_config.ini'
                    sh 'cp -f ../Doorstop/x86/winhttp.dll winhttp.dll'

                    sh "zip -r9 BepInEx_v4_x86_${shortCommit}.zip ./*"
                    
                    sh 'cp -f ../Doorstop/x64/winhttp.dll winhttp.dll'
                    
                    sh "zip -r9 BepInEx_v4_x64_${shortCommit}.zip ./* -x \\*.zip"
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
        always {
			withCredentials([string(credentialsId: 'discord-notify-webhook', variable: 'DISCORD_WEBHOOK')]) {
				discordSend description: "**Build:** [${currentBuild.id}](${env.BUILD_URL})\n**Status:** [${currentBuild.currentResult}](${env.BUILD_URL})\n\n**Artifacts:**\n- [BepInEx_v4_x86_${shortCommit}.zip](${env.BUILD_URL}artifact/Build/BepInEx_v4_x86_${shortCommit}.zip)\n- [BepInEx_v4_x64_${shortCommit}.zip](${env.BUILD_URL}artifact/Build/BepInEx_v4_x64_${shortCommit}.zip)", footer: 'Jenkins via Discord Notifier', link: env.BUILD_URL, successful: currentBuild.resultIsBetterOrEqualTo('SUCCESS'), title: "${env.JOB_NAME} #${currentBuild.id}", webhookURL: DISCORD_WEBHOOK
			}
        }
    }
}