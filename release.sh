
version=`python -m bump`
git commit -a -m "version bump"
git tag v$version

rm -r artifacts/HexManiac.WPF/bin
dotnet build -c Debug -p:Platform=x64
dotnet build -c Release -p:Platform=x64
cd artifacts/HexManiac.WPF/bin/Debug/net6.0-windows
zip ../../../../../HexManiacAdvance_x64.$version.debug.zip -r .
cd ../../Release/net6.0-windows
zip ../../../../../HexManiacAdvance_x64.$version.zip -r .
cd ../../../../..

rm -r artifacts/HexManiac.WPF/bin
dotnet build -c Release -p:Platform=x86
cd artifacts/HexManiac.WPF/bin/Release/net6.0-windows
zip ../../../../../HexManiacAdvance_x86.$version.zip -r .
cd ../../../../..

cp *.zip sampleFiles/in_flight/old_zips

echo $version
echo "use 'git push origin v$version' to publish this release to GitHub."
