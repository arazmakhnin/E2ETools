rm E2E-tools-win.zip
rm E2E-tools-linux.zip
rm E2E-tools-osx.zip

dotnet clean
dotnet warp -r win-x64
"c:\Program Files\7-Zip\7z.exe" a -tzip E2E-tools-win.zip E2ETools.exe options.json sample.yaml

rm E2ETools.exe

dotnet clean
dotnet warp -r linux-x64
"c:\Program Files\7-Zip\7z.exe" a -tzip E2E-tools-linux.zip E2ETools options.json sample.yaml

rm E2ETools

dotnet clean
dotnet warp -r osx-x64
"c:\Program Files\7-Zip\7z.exe" a -tzip E2E-tools-osx.zip E2ETools options.json sample.yaml

rm E2ETools