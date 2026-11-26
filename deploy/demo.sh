#!/usr/bin/env bash
# Демонстрация лаб 10.2–10.3 одной командой (запускать из папки ResumeBuilder):
#   bash deploy/demo.sh          — все части по очереди
#   bash deploy/demo.sh docker   — только 10.2 (один контейнер)
#   bash deploy/demo.sh compose  — только 10.3 (api + PostgreSQL + Redis)
#   bash deploy/demo.sh swarm    — только кластер Swarm
# Перед каждой командой скрипт показывает её текст и ждёт Enter — можно спокойно объяснять.

PART=${1:-all}
URL=http://localhost:8080
BLUE='\033[1;34m'; GREEN='\033[1;32m'; YELLOW='\033[1;33m'; NC='\033[0m'

title() { echo; echo -e "${YELLOW}==================== $* ====================${NC}"; }
pause() { echo; read -r -p "  [Enter — дальше] " _; }
run()   { echo; echo -e "${BLUE}\$ $*${NC}"; eval "$@"; }
wait_api() {
  echo -n "  ждём, пока служба ответит"
  for _ in $(seq 1 60); do
    if curl -fs "$URL/health" > /dev/null 2>&1; then echo -e " ${GREEN}готово${NC}"; return; fi
    echo -n "."; sleep 2
  done
  echo " — не дождались, смотрите журнал"
}

# Подготовка: убрать то, что могло остаться с прошлого показа
cleanup() {
  docker rm -f rb > /dev/null 2>&1
  docker compose down > /dev/null 2>&1
  docker stack rm resumebuilder > /dev/null 2>&1
  docker swarm leave --force > /dev/null 2>&1
  sleep 2
}

if ! docker info > /dev/null 2>&1; then
  echo "Docker не запущен. Откройте Docker Desktop и дождитесь «Engine running»."; exit 1
fi
cleanup

# ---------------------------------------------------------------- 10.2
if [[ $PART == all || $PART == docker ]]; then
  title "Лаба 10.2 — Docker-образ и контейнер"
  echo "Образ собран из src/ResumeBuilder.Api/Dockerfile (многоэтапная сборка), тег — версия программы."
  run "docker images resumebuilder-api"
  pause
  echo "Запуск контейнера. Настройка через переменную окружения: язык ответов — английский."
  run "docker run -d --name rb -p 8080:8080 -e App__DefaultLanguage=en-US resumebuilder-api:1.2.0"
  wait_api
  run "docker ps"
  pause
  echo "Проверка службы (то же, что /health в Swagger: $URL/swagger)"
  run "curl -s $URL/health"; echo
  run "docker logs rb"
  pause
  run "docker stop rb && docker rm rb"
  pause
fi

# ---------------------------------------------------------------- 10.3
if [[ $PART == all || $PART == compose ]]; then
  title "Лаба 10.3 — docker compose: служба + PostgreSQL + Redis"
  echo "Файл docker-compose.yml: три сервиса, своя сеть, том для базы, healthcheck."
  run "docker compose up -d"
  wait_api
  run "docker compose ps"
  pause
  echo "Служба подключилась к базе и кэшу:"
  run "curl -s $URL/health"; echo
  pause
  echo "Сохраняем резюме-пример в PostgreSQL и смотрим список:"
  run "curl -s $URL/api/sample -o /tmp/rb-sample.json"
  run "curl -s -X POST $URL/api/resumes -H 'Content-Type: application/json' --data @/tmp/rb-sample.json"; echo
  run "curl -s $URL/api/resumes"; echo
  pause
  echo "PDF дважды: второй раз берётся из кэша Redis (заголовок X-Cache):"
  run "curl -s -o /tmp/rb.pdf -D - -X POST $URL/api/pdf -H 'Content-Type: application/json' --data @/tmp/rb-sample.json | grep -i x-cache"
  run "curl -s -o /tmp/rb.pdf -D - -X POST $URL/api/pdf -H 'Content-Type: application/json' --data @/tmp/rb-sample.json | grep -i x-cache"
  echo "  PDF сохранён: /tmp/rb.pdf ($(wc -c < /tmp/rb.pdf) байт)"
  pause
  echo "Перезапуск всех контейнеров — данные остаются в томе resumebuilder-pgdata:"
  run "docker compose down"
  run "docker compose up -d"
  wait_api
  run "curl -s $URL/api/resumes"; echo
  pause
  run "docker compose logs --tail 5 api"
  run "docker compose down"
  pause
fi

# ---------------------------------------------------------------- Swarm
if [[ $PART == all || $PART == swarm ]]; then
  title "Модуль 3 — кластер Docker Swarm"
  run "docker swarm init > /dev/null && docker node ls"
  pause
  echo "Развёртываем стек из deploy/swarm/stack.yml (api в 3 репликах):"
  run "docker stack deploy -c deploy/swarm/stack.yml resumebuilder"
  echo -n "  ждём реплики"
  for _ in $(seq 1 60); do
    [[ $(docker service ls --filter name=resumebuilder_api --format '{{.Replicas}}') == "3/3" ]] && break
    echo -n "."; sleep 2
  done; echo
  run "docker service ls"
  pause
  echo "Масштабирование до 5 реплик:"
  run "docker service scale resumebuilder_api=5"
  run "docker service ps resumebuilder_api"
  pause
  echo "Запросы обрабатывают разные реплики — меняется поле host:"
  wait_api
  for _ in 1 2 3 4; do curl -s "$URL/health"; echo; done
  pause
  run "docker stack rm resumebuilder"
  run "docker swarm leave --force"
fi

echo; echo -e "${GREEN}Демонстрация закончена.${NC}"
