#!/usr/bin/env bash
set -euo pipefail

SIGNING_IDENTITY="ChatUnpack Local Signing"

if security find-identity -v -p codesigning \
  | grep -F "\"$SIGNING_IDENTITY\"" >/dev/null; then
  echo "本地代码签名身份已经存在：$SIGNING_IDENTITY"
  exit 0
fi

KEYCHAIN_PATH="$(security default-keychain -d user | tr -d ' \"')"
if [[ -z "$KEYCHAIN_PATH" || ! -f "$KEYCHAIN_PATH" ]]; then
  echo "无法确认当前用户的默认钥匙串" >&2
  exit 1
fi

CHATUNPACK_CERT_TMP="$(mktemp -d /tmp/chatunpack-cert.XXXXXX)"
cleanup_chatunpack_cert() {
  find "$CHATUNPACK_CERT_TMP" -type f -delete
  rmdir "$CHATUNPACK_CERT_TMP"
}
trap cleanup_chatunpack_cert EXIT

TEMP_IMPORT_PASSWORD="chatunpack-local-import"

/usr/bin/openssl req -new -newkey rsa:2048 -x509 -days 3650 -nodes -batch \
  -subj "/CN=$SIGNING_IDENTITY/O=zaynzhu/OU=Local Development" \
  -addext "basicConstraints=critical,CA:FALSE" \
  -addext "keyUsage=critical,digitalSignature" \
  -addext "extendedKeyUsage=codeSigning" \
  -keyout "$CHATUNPACK_CERT_TMP/private-key.pem" \
  -out "$CHATUNPACK_CERT_TMP/certificate.pem" >/dev/null 2>&1

/usr/bin/openssl pkcs12 -export \
  -inkey "$CHATUNPACK_CERT_TMP/private-key.pem" \
  -in "$CHATUNPACK_CERT_TMP/certificate.pem" \
  -name "$SIGNING_IDENTITY" \
  -passout "pass:$TEMP_IMPORT_PASSWORD" \
  -out "$CHATUNPACK_CERT_TMP/identity.p12"

security import "$CHATUNPACK_CERT_TMP/identity.p12" \
  -k "$KEYCHAIN_PATH" \
  -P "$TEMP_IMPORT_PASSWORD" \
  -T /usr/bin/codesign \
  -T /usr/bin/security

security add-trusted-cert \
  -r trustRoot \
  -p codeSign \
  -k "$KEYCHAIN_PATH" \
  "$CHATUNPACK_CERT_TMP/certificate.pem"

security find-identity -v -p codesigning "$KEYCHAIN_PATH"
echo "已创建本地代码签名身份：$SIGNING_IDENTITY"
