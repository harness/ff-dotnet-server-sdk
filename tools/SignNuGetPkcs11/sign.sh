#!/usr/bin/env bash

if [[ ! -n "$1" || ! -n "$2" ]]; then
  echo "Usage: $0 <nuget_package> <cert_fingerprint>"
  exit 1
fi

if [ ! -n "$SM_API_KEY" ]; then
  echo "SM_API_KEY env var is not set"
  exit 1
fi

if [ ! -n "$SM_CLIENT_CERT_FILE" ]; then
  echo "SM_CLIENT_CERT_FILE env var is not set"
  exit 1
fi

if [ ! -n "$SM_CLIENT_CERT_PASSWORD" ]; then
  echo "SM_CLIENT_CERT_PASSWORD env var is not set"
  exit 1
fi

PKG_PATH=$1
FINGER_PRINT=$2

dotnet build

if [ ! -f smtools-linux-x64.tar.gz ]; then
    wget https://one.digicert.com/signingmanager/api-ui/v1/releases/noauth/smtools-linux-x64.tar.gz/download -O smtools-linux-x64.tar.gz
fi

tar -xzf smtools-linux-x64.tar.gz

export SM_LOG_OUTPUT=stdout
export SM_LOG_LEVEL=TRACE
export SM_HOST=https://clientauth.one.digicert.com/

chmod +x smtools-linux-x64/smctl

./smtools-linux-x64/smctl healthcheck

dotnet run --file $PKG_PATH --fingerprint $FINGER_PRINT --pkcs11-lib smtools-linux-x64/smpkcs11.so

rm -rf smtools-linux-x64

dotnet nuget verify $PKG_PATH
