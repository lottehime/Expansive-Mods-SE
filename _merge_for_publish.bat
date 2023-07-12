@echo off

echo ==================================
echo Expansive Mods - Merge For Publish
echo ==================================
echo.

pause
echo.

echo Beginning mod merge.
echo.

echo Cleaning up from any previous operations...
RD /S /Q .\Combined
echo.

echo Creating Combined folder if needed...
md Combined
echo.

echo Moving to Mods directory to process files.
echo.

cd .\Mods

echo Processing files and merging into Combined folder...
echo.
echo Files processed:
echo -----------------
for /f "delims=" %%f in ('dir /b /ad') do (
    if not %%f==Combined xcopy "%%f" ..\Combined\ExpansiveMods /I /E /Y
    echo.
)
echo.

xcopy ..\images\thumb_small.png ..\Combined\ExpansiveMods\thumb.png /I /E /Y
echo.

xcopy ..\LICENSE.txt ..\Combined\ExpansiveMods
echo.

xcopy ..\README.MD ..\Combined\ExpansiveMods
echo.

echo Operation complete.
echo.

pause