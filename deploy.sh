cd AIChat

dotnet publish

cd bin/Release/net9.0/publish/

zip -r publish.zip .

az webapp deploy --resource-group ScaleForce.PRDBot --name excitel-prdbot --src-path "publish.zip"
