cd AIChat

dotnet publish

cd bin/Release/net9.0/publish/

zip -r publish.zip .

az webapp deploy --resource-group AIChat --name ExcitelAIChat --src-path "publish.zip"
