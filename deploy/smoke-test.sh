#!/usr/bin/env bash
# Проверка работающей службы: ./deploy/smoke-test.sh [адрес]
set -euo pipefail
URL=${1:-http://localhost:8080}

echo "Ждём, пока служба ответит..."
for i in $(seq 1 60); do
  if curl -fs "$URL/health" > /dev/null; then break; fi
  sleep 2
done

echo "1. /health";            curl -fs "$URL/health"; echo
echo "2. пример резюме";      curl -fs "$URL/api/sample" -o /tmp/sample.json
echo "3. проверка резюме";    curl -fs -X POST "$URL/api/validate" -H "Content-Type: application/json" --data @/tmp/sample.json; echo
echo "4. PDF";                curl -fs -D /tmp/h1 -X POST "$URL/api/pdf" -H "Content-Type: application/json" --data @/tmp/sample.json -o /tmp/r1.pdf
head -c 4 /tmp/r1.pdf | grep -q "%PDF" || { echo "ОШИБКА: ответ не PDF"; exit 1; }
echo "   PDF получен: $(wc -c < /tmp/r1.pdf) байт, $(grep -i x-cache /tmp/h1 | tr -d '\r')"
echo "5. тот же PDF ещё раз"; curl -fs -D /tmp/h2 -X POST "$URL/api/pdf" -H "Content-Type: application/json" --data @/tmp/sample.json -o /tmp/r2.pdf
grep -qi "x-cache: HIT" /tmp/h2 || { echo "ОШИБКА: второй запрос не из кэша"; exit 1; }
echo "   взят из кэша (X-Cache: HIT)"
echo "6. сохранение";         curl -fs -X POST "$URL/api/resumes" -H "Content-Type: application/json" --data @/tmp/sample.json; echo
echo "7. список";             curl -fs "$URL/api/resumes"; echo
echo "8. QR-код";             curl -fs "$URL/api/qr?text=https://github.com" -o /tmp/qr.png && echo "   PNG: $(wc -c < /tmp/qr.png) байт"
echo "Все проверки пройдены"
