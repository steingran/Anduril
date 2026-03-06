# Medium cookie authentication

Anduril's `MediumArticleTool` can use your existing Medium subscription by sending the same `Cookie` header your browser sends when you're logged in.

## Configure it safely

Do **not** commit your Medium cookie to `appsettings.json`.

Use an environment variable instead:

```powershell
$env:Integrations__MediumArticle__CookieHeader = 'cookie1=value1; cookie2=value2'
```

Then start the host from the same shell session.

## Optional startup cookie validation

You can also configure a validation URL so Anduril checks your Medium cookie at startup.

Set a Medium article URL that:

- is hosted on Medium
- is normally subscriber-only
- you can personally open while logged in

Example:

```powershell
$env:Integrations__MediumArticle__ValidationUrl = 'https://medium.com/@author/member-only-article'
```

When both `CookieHeader` and `ValidationUrl` are configured, Anduril will fetch that page during startup:

- if it still looks paywalled, Anduril logs a warning that your cookie may be stale, expired, or incomplete
- if the fetch fails, Anduril logs a warning and continues startup
- if the page loads normally, validation succeeds silently apart from an informational log entry

## How to copy the cookie from your browser

The safest approach is to copy the full `Cookie` request header from an already-authenticated Medium article request.

### Chrome or Edge

1. Sign in to Medium in your browser.
2. Open a Medium article that your subscription can read.
3. Open Developer Tools with `F12`.
4. Go to the **Network** tab.
5. Refresh the page.
6. Click the top-level document request for the article page.
7. In **Headers**, find the `cookie` request header.
8. Copy the full header value only, not the word `cookie:`.

### Firefox

1. Sign in to Medium.
2. Open a subscribed article.
3. Press `F12` and open **Network**.
4. Refresh the page.
5. Select the article document request.
6. In **Headers**, copy the full `Cookie` header value.

## Important notes

- Copy the **entire** cookie header value.
- Cookie values expire. If Medium starts returning paywalled previews again, refresh the cookie.
- If you configured `ValidationUrl`, use a subscriber-only article you know should open successfully with your account.
- Treat this like a password. Anyone with the cookie may be able to use your authenticated Medium session.
- If possible, prefer a shell environment variable over storing the cookie in any tracked file.