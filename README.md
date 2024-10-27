This is part of this blog post I made: [Fullstack Serverless Blazor App With Azure Functions](https://eecs.blog/fullstack-serverless-blazor-app-with-azure-functions/)

I made a very simple cloud files storage app to demonstrate how to make a Blazor C# WebAssembly app with a serverless backend utilizing Azure Functions.


The local.settings.json is .gitignore-ed for security reasons as it is used for storing envars. So here are its file contents:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true", 
    "FUNCTIONS_INPROC_NET8_ENABLED": "1",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  },
  "Host": {
    "CORS": "*"
  }
}
```

**Note:** Replace "UseDevelopmentStorage=true" of the AzureWebJobsStorage property with the connection string to your storage account.
