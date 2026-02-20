# Microsoft Teams Setup for Anduril

This guide walks you through setting up Anduril to communicate via Microsoft Teams using the Bot Framework.

---

## Prerequisites

- An Azure account with permissions to create App Registrations
- A Microsoft Teams workspace where you can install custom apps
- Anduril running and accessible via HTTPS (required for Bot Framework webhooks)
- For local development: Azure Dev Tunnels CLI (`winget install Microsoft.devtunnel`)

---

## Step 1: Create an Azure Bot Registration

1. **Go to the Azure Portal**: https://portal.azure.com
2. **Create a new Azure Bot**:
   - Search for "Azure Bot" in the marketplace
   - Click **Create**
   - Fill in the required fields:
     - **Bot handle**: Choose a unique name (e.g., `anduril-bot`)
     - **Subscription**: Select your Azure subscription
     - **Resource group**: Create new or use existing
     - **Pricing tier**: F0 (free tier is sufficient for development)
     - **Microsoft App ID**: Select "Create new Microsoft App ID"
   - Click **Review + create**, then **Create**

3. **Note your credentials**:
   - After creation, go to the bot resource
   - Navigate to **Configuration** in the left menu
   - Copy the **Microsoft App ID** — you'll need this for Anduril configuration
   - Click **Manage** next to the Microsoft App ID to create a client secret

4. **Create a client secret**:
   - In the App Registration page, go to **Certificates & secrets**
   - Click **New client secret**
   - Add a description (e.g., "Anduril bot secret")
   - Choose an expiration period
   - Click **Add**
   - **Copy the secret value immediately** — you won't be able to see it again

---

## Step 2: Configure the Messaging Endpoint

1. **In the Azure Bot resource**, go to **Configuration**
2. **Set the Messaging endpoint**:
   - Enter your Anduril webhook URL: `https://your-anduril-host.com/api/teams/messages`
   - Replace `your-anduril-host.com` with your actual domain
   - For local development with Azure Dev Tunnels: `https://abc123-5000.euw.devtunnels.ms/api/teams/messages`
3. Click **Apply**

---

## Step 3: Enable the Teams Channel

1. **In the Azure Bot resource**, go to **Channels**
2. Click on **Microsoft Teams** icon
3. Click **Apply** to enable the Teams channel
4. The Teams channel should now show as "Running"

---

## Step 4: Configure Anduril

Add your Teams credentials to Anduril's configuration:

### Using User Secrets (Recommended for Development)

```bash
cd src/Anduril.Host
dotnet user-secrets set "Communication:Teams:MicrosoftAppId" "your-app-id-here"
dotnet user-secrets set "Communication:Teams:MicrosoftAppPassword" "your-client-secret-here"
```

### Using appsettings.json (Not Recommended for Production)

Edit `src/Anduril.Host/appsettings.json`:

```json
{
  "Communication": {
    "Teams": {
      "MicrosoftAppId": "your-app-id-here",
      "MicrosoftAppPassword": "your-client-secret-here"
    }
  }
}
```

### Using Environment Variables (Recommended for Production)

```bash
export Communication__Teams__MicrosoftAppId="your-app-id-here"
export Communication__Teams__MicrosoftAppPassword="your-client-secret-here"
```

---

## Step 5: Install the Bot in Teams

1. **In the Azure Bot resource**, go to **Channels**
2. Click on **Microsoft Teams** channel
3. Click **Open in Teams** — this will open Teams and prompt you to add the bot
4. Click **Add** to install the bot to your personal scope
5. Alternatively, click **Add to a team** to install it in a specific channel

---

## Step 6: Test the Integration

1. **Start Anduril**:
   ```bash
   cd src/Anduril.Host
   dotnet run
   ```

2. **Send a message to the bot in Teams**:
   - Open the chat with your bot in Teams
   - Type a message like "Hello Anduril"
   - You should receive a response from Anduril

3. **Check the logs**:
   - Anduril logs should show:
     ```
     Teams adapter started. Waiting for Bot Framework webhook messages...
     ```
   - When you send a message, you should see processing logs

---

## Local Development with Azure Dev Tunnels

For local development, use Azure Dev Tunnels to expose your local Anduril instance to the internet. It's free, has no session time limits, and is built by Microsoft.

1. **Install the CLI**:
   ```bash
   winget install Microsoft.devtunnel
   ```

2. **Log in** (uses your Microsoft/Azure account):
   ```bash
   devtunnel user login
   ```

3. **Start a tunnel** on Anduril's port:
   ```bash
   devtunnel host -p 5000 --allow-anonymous
   ```
   The CLI will print a public HTTPS URL, e.g. `https://abc123-5000.euw.devtunnels.ms`.

   **Optional — persistent named tunnel** (keeps the same URL across restarts):
   ```bash
   devtunnel create anduril-bot
   devtunnel port create anduril-bot -p 5000
   devtunnel host anduril-bot
   ```

4. **Update the Azure Bot messaging endpoint**:
   - Go to Azure Portal → Your Bot → Configuration
   - Set Messaging endpoint to: `https://<your-tunnel-url>/api/teams/messages`
   - Click **Apply**

5. **Start Anduril** and test as described above

---

## Troubleshooting

### Bot doesn't respond to messages

- **Check Anduril logs** for errors
- **Verify the messaging endpoint** in Azure matches your Anduril URL
- **Ensure HTTPS** — Bot Framework requires HTTPS (use Azure Dev Tunnels for local dev)
- **Check credentials** — Make sure MicrosoftAppId and MicrosoftAppPassword are correct

### "Unauthorized" errors in logs

- **Verify the client secret** hasn't expired
- **Regenerate the secret** if needed and update Anduril configuration

### Messages not formatted correctly

- Anduril converts standard Markdown to Teams-compatible format automatically
- Teams supports most standard Markdown (headers, bold, italic, links, code blocks)

---

## Production Deployment

For production deployment:

1. **Use a proper domain** with a valid SSL certificate
2. **Store credentials securely** using Azure Key Vault or environment variables
3. **Enable authentication** in your hosting environment
4. **Monitor logs** for errors and performance issues
5. **Set up alerts** for bot downtime or errors

---

## Additional Resources

- [Bot Framework Documentation](https://docs.microsoft.com/en-us/azure/bot-service/)
- [Teams Bot Development](https://docs.microsoft.com/en-us/microsoftteams/platform/bots/what-are-bots)
- [Anduril Documentation](../README.md)

