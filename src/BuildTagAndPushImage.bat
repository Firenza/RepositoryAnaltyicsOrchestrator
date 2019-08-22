docker build -t firenza/repository_analytics_orchestrator:latest .
docker image prune -f
docker push firenza/repository_analytics_orchestrator:latest
pause
exit