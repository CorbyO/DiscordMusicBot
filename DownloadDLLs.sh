#!/bin/sh

# if not windows. exit
if [ "$OS" != "Windows_NT" ]; then
  exit 0
fi

OPUS_AND_LIBSODIUM_64="https://dsharpplus.github.io/natives/vnext_natives_win32_x64.zip"
OPUS_AND_LIBSODIUM_32="https://dsharpplus.github.io/natives/vnext_natives_win32_x86.zip"
OS_BIT=$(getconf LONG_BIT)

# download opus and sodium
if [ ! -f ./opus.dll ]; then
  echo "Downloading opus.dll"
  if [ "$OS_BIT" == "64" ]; then
    curl -o oas.zip -O $OPUS_AND_LIBSODIUM_64
  else
    curl -o oas.zip -O $OPUS_AND_LIBSODIUM_32
  fi
  unzip oas.zip
  rm oas.zip
  rm -rf ./*.dll.md*
  rm -rf ./*.dll.sha*
  mv ./libopus.dll ./opus.dll
fi
