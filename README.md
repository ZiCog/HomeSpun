HomeSpun
========

HomeSpun is a compiler for the Spin language as used on the Propeller micro-controller from Parallax Inc.

Author: Michael Park.

License: Michael was most generous in releasing HomeSpun under the MIT license see the announcement here:

http://forums.parallax.com/showthread.php/106401-Homespun-Spin-compiler-0.31-Now-open-source

For further information check the wikispaces page:

http://propeller.wikispaces.com/Homespun+Spin+Compiler

This repo is created from the  VS2012 project for HomeSpun provided by Batang on the Parallax forums:

http://forums.parallax.com/showthread.php/148260-HomeSpun-Compiler-Open-Source


Build - Debian
--------------

HomeSpun will compile on Debian using the Mono tool chain.

  $ sudo apt-get install mono-complete
  $ cd HomeSpun
  $ xbuild HomeSpun.csproj
  
Will produce executables  HomeSpun/bin/Debug/HomeSpun.exe and
HomeSpun/bin/Debug/HomeSpun.exe


