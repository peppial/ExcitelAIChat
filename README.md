# AI Chat with Custom Data

This project is an AI chat application using RAG and an MCP Server based on aichatweb.

### Required Environment Variables

#### Azure OpenAI Configuration
- **AZURE_OPENAI_ENDPOINT**: Your Azure OpenAI service endpoint
  - Example: `https://your-resource.openai.azure.com/`
- **AZURE_OPENAI_API_KEY**: Your Azure OpenAI API key
  - Example: `your-azure-openai-api-key-here`

#### Azure AI Search Configuration
- **AZURE_SEARCH_ENDPOINT**: Your Azure AI Search service endpoint
  - Example: `https://your-search-service.search.windows.net`
- **AZURE_SEARCH_API_KEY**: Your Azure AI Search admin key
  - Example: `your-azure-search-admin-key-here`

#### Authentication Configuration
- **LOGIN_USERNAME**: Username for basic authentication (if authentication is enabled)
  - Example: `admin`
- **LOGIN_PASSWORD**: Password for basic authentication (if authentication is enabled)
  - Example: `your-secure-password`


### Azure Resource Setup

#### 1. Azure OpenAI Service
1. Create an Azure OpenAI resource in the Azure portal
2. Deploy the following models:
   - **gpt-4o-mini** (for chat)
   - **text-embedding-3-small** (for embeddings)
3. Copy the endpoint and API key from the resource

#### 2. Azure AI Search Service
1. Create an Azure AI Search service in the Azure portal
2. Choose an appropriate pricing tier (Basic or higher recommended)
3. Copy the endpoint and admin key from the resource
4. Create an index with a vector index https://learn.microsoft.com/en-us/azure/search/vector-search-how-to-create-index

### Data Ingestion

The application automatically ingests documents from the `wwwroot/Data` directory on startup. Supported formats:
- **PDF files** (.pdf)
- **Word documents** (.docx)

Place your documents in the `wwwroot/Data` folder and restart the application to ingest them.


### How to start from scratch with the aichatweb template:

```
dotnet new install Microsoft.Extensions.AI.Templates
dotnet new aichatweb
```
