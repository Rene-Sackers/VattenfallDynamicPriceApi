@echo off
docker compose -f compose-gen-client.yaml up --build --abort-on-container-exit
docker compose -f compose-gen-client.yaml down