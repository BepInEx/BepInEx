/*
*  BepInEx Bleeding Edge build CI jenkinsfile
*/
lastBuildCommit = ''

pipeline {
    agent any
    parameters {
        // Check if the build is Bleeding Edge. Affects whether the result is pushed to BepisBuilds
        booleanParam(name: 'IS_BE')
    }
    stages {
        stage('Pull Projects') {
            steps {
                script {
                    if (currentBuild.previousBuild != null && currentBuild.previousBuild.buildVariables.containsKey('LAST_BUILD')) {
                        lastBuildCommit = currentBuild.previousBuild.buildVariables['LAST_BUILD']
                    }
                }

                // Clean up old project before starting
                cleanWs()

                dir('BepInEx') {
                    git 'https://github.com/BepInEx/BepInEx.git'

                    script {
                        longCommit = sh(returnStdout: true, script: 'git rev-parse HEAD').trim()
                    }
                }
            }
        }
        stage('Build BepInEx') {
            steps {
                dir('BepInEx') {
                    sh 'chmod u+x build.sh'
                    withCredentials([string(credentialsId: 'BEPINEX_NUGET_KEY', variable: 'BEPINEX_NUGET_KEY')]) {
                        sh "./build.sh --target=Pack --bleeding_edge=${params.IS_BE} --build_id=${currentBuild.id} --last_build_commit=${lastBuildCommit} --nuget_push_key=${BEPINEX_NUGET_KEY}"
                    }
                }
            }
        }
        stage('Package') {
            steps {
                dir('BepInEx/bin/dist') {
                    archiveArtifacts '*.zip'
                }
            }
        }
    }
    post {
        cleanup {
            script {
                env.LAST_BUILD = lastBuildCommit
            }
        }
        success {
            script {
                lastBuildCommit = longCommit
                if (params.IS_BE) {
                    dir('BepInEx/bin/dist') {
                        def filesToSend = findFiles(glob: '*.*').collect { it.name }
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
