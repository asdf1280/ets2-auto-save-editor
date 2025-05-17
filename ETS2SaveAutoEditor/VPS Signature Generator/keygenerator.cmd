@echo off
set "O=C:\msys64\usr\bin\openssl.exe"

rem 1. generate 2048-bit private key (PEM)
%O% genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out private.pem

rem 2. extract public key in DER
%O% rsa -in private.pem -pubout -outform DER -out public.der

rem 3a. single-line Base64 with OpenSSL
%O% base64 -in public.der -A -out public.der.b64.txt