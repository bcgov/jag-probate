#!/bin/bash

echo "Generating runtime config..."
/opt/app-root/src/generate-runtime-config.sh /opt/app-root/src/public/config.json

echo "Starting Vite dev server..."
npm run dev
