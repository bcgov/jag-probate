#!/bin/bash

set -eu

OUTPUT_PATH=${1:?Output path is required}
DEFAULT_BCEID_REGISTER_URL='https://www.bceid.ca/register/basic/account_details.aspx?type=regular&serviceId=7493&eServiceType=all'

json_escape() {
  printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g' -e ':a;N;$!ba;s/\n/\\n/g'
}

environment_value=${APP_ENVIRONMENT:-dev}
bceid_register_url=${BCEID_REGISTER_URL:-$DEFAULT_BCEID_REGISTER_URL}

mkdir -p "$(dirname "$OUTPUT_PATH")"

cat > "$OUTPUT_PATH" <<EOF
{
  "environment": "$(json_escape "$environment_value")",
  "bceidRegisterUrl": "$(json_escape "$bceid_register_url")"
}
EOF
