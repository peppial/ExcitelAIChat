# AI Chat with Custom Data

This project is an AI chat application that demonstrates how to chat with custom data using an AI language model. Please note that this template is currently in an early preview stage. If you have feedback, please take a [brief survey](https://aka.ms/dotnet-chat-templatePreview2-survey).



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

#### Authentication Configuration (Optional)
- **LOGIN_USERNAME**: Username for basic authentication (if authentication is enabled)
  - Example: `admin`
- **LOGIN_PASSWORD**: Password for basic authentication (if authentication is enabled)
  - Example: `your-secure-password`

#### GitHub Models Configuration (Alternative to Azure OpenAI)
- **GITHUB_MODELS_TOKEN**: GitHub personal access token with `models:read` permissions
  - Example: `github_pat_your-token-here`

### Setting Environment Variables

#### Option 1: Using .NET User Secrets (Recommended for Development)

From the command line, configure your secrets for this project by running the following commands:

```sh
cd /Users/penkaaleksandrova/Projects/AIChat/AIChat
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-azure-openai-api-key-here"
dotnet user-secrets set "AzureSearch:Endpoint" "https://your-search-service.search.windows.net"
dotnet user-secrets set "AzureSearch:ApiKey" "your-azure-search-admin-key-here"
dotnet user-secrets set "Auth:Username" "admin"
dotnet user-secrets set "Auth:Password" "your-secure-password"
```

#### Option 2: Using Environment Variables

**On macOS/Linux:**
```sh
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-azure-openai-api-key-here"
export AZURE_SEARCH_ENDPOINT="https://your-search-service.search.windows.net"
export AZURE_SEARCH_API_KEY="your-azure-search-admin-key-here"
export LOGIN_USERNAME="admin"
export LOGIN_PASSWORD="your-secure-password"
```

**On Windows (PowerShell):**
```powershell
$env:AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY="your-azure-openai-api-key-here"
$env:AZURE_SEARCH_ENDPOINT="https://your-search-service.search.windows.net"
$env:AZURE_SEARCH_API_KEY="your-azure-search-admin-key-here"
$env:LOGIN_USERNAME="admin"
$env:LOGIN_PASSWORD="your-secure-password"
```

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
4. The application will automatically create the required index (`aichat-index`)

### Data Ingestion

The application automatically ingests documents from the `wwwroot/Data` directory on startup. Supported formats:
- **PDF files** (.pdf)
- **Word documents** (.docx)

Place your documents in the `wwwroot/Data` folder and restart the application to ingest them.
