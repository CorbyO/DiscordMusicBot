#!/bin/sh

ls
if [ -z "$1" ]
  then
    echo "No argument supplied."
    echo "Usage: PostProcessor.sh <build directory>"
else
  if [ ! -d $1 ]; then
    mkdir $1
  fi
  cp -f ./config.json $1 &&
  echo "Copying config file to $1"
fi