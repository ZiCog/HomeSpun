HomeSpun
========

HomeSpun is a compiler for the Spin language as used on the Propeller micro-controller from Parallax Inc.

Author: Michael Park.

License: Michael was most generous in releasing HomeSpun under the MIT license see the announcement here:

http://forums.parallax.com/showthread.php/106401-Homespun-Spin-compiler-0.31-Now-open-source

For further information check the wikispaces page:

http://propeller.wikispaces.com/Homespun+Spin+Compiler

This repo is created from the  VS2012 project for HomeSpun v0.32 provided by Batang on the Parallax forums:

http://forums.parallax.com/showthread.php/148260-HomeSpun-Compiler-Open-Source

NOTE: There is no plan for future development on this repo. There is a development repo on Google Code:

http://code.google.com/p/homespun-spin-compiler/


Build - Debian
--------------

HomeSpun will compile on Debian using the Mono tool chain.

  $ sudo apt-get install mono-complete

  $ cd HomeSpun

  $ xbuild HomeSpun.csproj
  
Will produce executables  HomeSpun/bin/Debug/HomeSpun.exe and
HomeSpun/bin/Debug/HomeSpun.exe

Run with:

$ mono HomeSpun.exe
  
Build - Windows
---------------

No idea. I guess you just hit a button in VS012 somewhere. 

Run on Raspberry Pi !
---------------------

HomeSpun.exe runs fine on the Raspi only requiring a couple of mono packages (No need to build it there of course):

  $ sudo apt-get install mono-runtime
  $ sudo apt-get install libmono-corlib2.0-cil
  $ mono HomeSpun.exe
  
  



