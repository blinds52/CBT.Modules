@ECHO OFF
msbuild /v:m /m /nologo dirs.proj /t:Rebuild

FOR /F "usebackq" %%A IN (`dir %~dp0*.nuspec /s /b ^| findstr /v /i "CBT.DotNetFx"`) DO (
	CALL :Pack "%%A"
)

GOTO :EOF

:Pack

"NuGet.exe" pack "%~1" -OutputDirectory %~dp0 -Properties "BinDir=%~dp0..\bin\Debug\AnyCPU\%~n1"