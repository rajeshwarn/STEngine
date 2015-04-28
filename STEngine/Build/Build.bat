@echo off

REM The %~dp0 specifier resolves to the path to the directory where this .bat is located in.
REM We use this so that regardless of where the .bat file was executed from, we can change to
REM directory relative to where we know the .bat is stored.

REM pushd "%~dp0\..\Source"

REM %1 is the game name
REM %2 is the platform name
REM %3 is the configuration name

IF EXIST ..\..\Binaries\DotNET\STBuildTool.exe (
         ..\..\Binaries\DotNET\STBuildTool.exe %* -DEPLOY
REM		 popd
) ELSE (
	ECHO STBuildTool.exe not found in ..\Engine\Binaries\DotNET\STBuildTool.exe 
	popd
	EXIT /B 999
)
