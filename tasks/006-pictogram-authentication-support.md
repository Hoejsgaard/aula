# Task 006: Pictogram Authentication Support

**Status:** READY FOR IMPLEMENTATION 📋
**Priority:** High
**Created:** 2025-09-25
**Type:** Feature Enhancement
**Investigation:** Complete
**Testing:** Partial (authentication flow mapped, pictogram interface not reached)

## Problem Statement

Younger children (2nd grade and below) use pictogram-based authentication instead of alphanumeric passwords. They select a sequence of images rather than typing a password. The current authentication system only supports text-based passwords.

### Authentication Process
- Enter username in text field
- Select pictograms in correct sequence (images are clicked in order)
- Click "Næste" (Next) button to submit

## Investigation Results

### Actual Authentication Flow (Tested)
1. **Initial Request:** GET to `https://www.minuddannelse.net/KmdIdentity/Login?domainHint=unilogin-idp-prod&toFa=False`
2. **SAML Redirect:** Redirects to `https://broker.unilogin.dk/auth/realms/broker/protocol/saml-stil` with SAMLRequest
3. **Login Selector:** Shows three options:
   - Unilogin (button with value="uni_idp")
   - MitID (button with value="nemlogin3")
   - Lokalt login (local login)
4. **After Selecting Unilogin:** Should show username/password or username/pictogram interface
   - Currently getting error page due to session/security issues
5. **Pictogram Interface:** Not yet reached in testing, but expected after username entry

### Current System Behavior
1. `UniLoginClient` processes HTML forms and submits username/password
2. `BuildFormData` method identifies text input fields and fills them
3. System expects both username and password as strings in configuration
4. No support for login selector page or pictogram authentication

### Pictogram Authentication Requirements
1. **Login Selector Handling:** Must first select "Unilogin" option from login selector page
2. **Session Management:** Maintain cookies and session state through redirects
3. **Detection:** Identify when the login page shows pictogram interface (after username)
4. **Parsing:** Extract available pictogram choices from HTML
5. **Selection:** Simulate clicks on pictogram icons in correct sequence
6. **Submission:** Click "Næste" (Next) button to complete authentication

### Technical Analysis
The pictogram interface differs significantly from standard forms:
- Username field remains standard text input
- Password replaced with clickable image grid
- Images have identifiable attributes (alt text, class names, data attributes)
- Selection likely triggers JavaScript events
- Form submission may be AJAX-based rather than standard POST

## Proposed Solution

### Configuration Structure

```csharp
// Option 1: Enhanced UniLogin class with authentication type
public class UniLogin
{
    public string Username { get; set; } = string.Empty;
    public AuthenticationType AuthType { get; set; } = AuthenticationType.Standard;
    public string Password { get; set; } = string.Empty;  // For standard auth
    public string[]? PictogramSequence { get; set; }  // For pictogram auth
}

public enum AuthenticationType
{
    Standard,    // Traditional alphanumeric password
    Pictogram    // Image-based authentication
}
```

```json
// appsettings.json example
{
  "MinUddannelse": {
    "Children": [
      {
        "FirstName": "TestChild2",
        "LastName": "Højsgaard",
        "UniLogin": {
          "Username": "soer51f3",
          "AuthType": "Standard",
          "Password": "Mærke878"
        }
      },
      {
        "FirstName": "Example Child",
        "LastName": "With Pictograms",
        "UniLogin": {
          "Username": "exampleuser",
          "AuthType": "Pictogram",
          "PictogramSequence": ["pictogram1", "pictogram2", "pictogram3", "pictogram4"]
        }
      }
    ]
  }
}
```

### Implementation Plan

#### Phase 1: Enhance Configuration
1. Update `UniLogin` class with authentication type
2. Add `PictogramSequence` property for image-based passwords
3. Update configuration validation to handle both types

#### Phase 2: Create PictogramAuthenticator
```csharp
public class PictogramAuthenticator : UniLoginClient
{
    private readonly string[] _pictogramSequence;

    protected override async Task<bool> ProcessLoginResponseAsync(string content)
    {
        // 1. Detect pictogram interface
        if (IsPictogramLoginPage(content))
        {
            // 2. Parse available pictograms
            var availablePictograms = ParsePictograms(content);

            // 3. Select pictograms in sequence
            foreach (var pictogram in _pictogramSequence)
            {
                await SelectPictogram(pictogram, availablePictograms);
            }

            // 4. Submit selection
            return await SubmitPictogramSelection();
        }

        // Fall back to standard flow
        return await base.ProcessLoginResponseAsync(content);
    }

    private Dictionary<string, HtmlNode> ParsePictograms(string html)
    {
        // Extract pictogram elements with their identifiers
        // Look for: alt text, data-pictogram, class names, etc.
    }

    private async Task SelectPictogram(string pictogramName, Dictionary<string, HtmlNode> available)
    {
        // Simulate click on pictogram
        // May need to trigger JavaScript events
    }
}
```

#### Phase 3: Update PerChildMinUddannelseClient
```csharp
public class PerChildMinUddannelseClient : IMinUddannelseClient
{
    private ChildAuthenticatedClient CreateAuthenticatedClient(Child child)
    {
        if (child.UniLogin?.AuthType == AuthenticationType.Pictogram)
        {
            return new PictogramChildAuthenticatedClient(
                child,
                child.UniLogin.Username,
                child.UniLogin.PictogramSequence,
                _logger
            );
        }

        // Standard authentication
        return new StandardChildAuthenticatedClient(
            child,
            child.UniLogin.Username,
            child.UniLogin.Password,
            _logger
        );
    }
}
```

#### Phase 4: HTML Parsing Strategy
```csharp
private bool IsPictogramLoginPage(string content)
{
    var doc = new HtmlDocument();
    doc.LoadHtml(content);

    // Detection markers:
    // 1. Look for pictogram container
    var pictogramGrid = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'pictogram-grid')]");

    // 2. Check for image selection elements
    var selectableImages = doc.DocumentNode.SelectNodes("//img[@data-pictogram or @data-selectable]");

    // 3. Absence of password field
    var passwordField = doc.DocumentNode.SelectSingleNode("//input[@type='password']");

    return (pictogramGrid != null || selectableImages?.Count > 0) && passwordField == null;
}

private async Task<bool> HandlePictogramAuthentication(HtmlDocument doc, string[] pictogramSequence)
{
    // Find all selectable pictograms
    var pictograms = doc.DocumentNode.SelectNodes("//img[@alt or @data-pictogram or @class]");

    foreach (var pictogramName in pictogramSequence)
    {
        // Find matching pictogram by alt text or data attribute
        var pictogram = pictograms?.FirstOrDefault(img =>
            img.GetAttributeValue("alt", "").ToLower().Contains(pictogramName) ||
            img.GetAttributeValue("data-pictogram", "").ToLower() == pictogramName ||
            img.GetAttributeValue("class", "").ToLower().Contains(pictogramName)
        );

        if (pictogram != null)
        {
            // Get click handler or form data
            var clickHandler = pictogram.GetAttributeValue("onclick", "");
            var pictogramId = pictogram.GetAttributeValue("id", "");

            // Simulate selection (may need JavaScript execution)
            await SimulatePictogramClick(pictogramId);
        }
    }

    return await SubmitPictogramForm();
}
```

## Challenges & Considerations

### Technical Challenges
1. **Login Selector Page:** Current implementation doesn't handle the initial login selector
2. **Session Management:** Complex SAML flow with session codes and signatures
3. **JavaScript Dependency:** Pictogram selection likely uses JavaScript events
4. **Dynamic Content:** Images may be loaded dynamically via AJAX
5. **Security Measures:** Server may detect and block automated access attempts
6. **Form Submission:** May use non-standard submission methods

### Potential Solutions
1. **Selenium/Playwright:** Use browser automation for JavaScript execution
2. **API Reverse Engineering:** Find direct API endpoints for pictogram auth
3. **JavaScript Evaluation:** Execute JavaScript within HttpClient context

### Security Considerations
- Pictogram sequences should be encrypted in configuration
- Consider using SecureString or environment variables
- Avoid logging pictogram sequences

## Testing Requirements
1. Create test account with pictogram authentication
2. Verify pictogram detection logic
3. Test sequence selection and submission
4. Validate successful authentication
5. Ensure fallback to standard auth works

## Files to Modify
- `src/Aula/Configuration/UniLogin.cs` - Add authentication type
- `src/Aula/Configuration/Child.cs` - Update references
- `src/Aula/Integration/UniLoginClient.cs` - Add pictogram detection
- `src/Aula/Integration/PerChildMinUddannelseClient.cs` - Route to correct authenticator
- `src/Aula/Integration/PictogramAuthenticator.cs` - New file
- `appsettings.json` - Update configuration structure
- `appsettings.example.json` - Document new structure

## Alternative Approach: Browser Automation

If direct HTTP requests prove insufficient due to JavaScript requirements:

```csharp
public class BrowserBasedAuthenticator
{
    private readonly IWebDriver _driver;

    public async Task<string> AuthenticateAndGetCookies(Child child)
    {
        _driver.Navigate().GoToUrl(loginUrl);

        // Enter username
        _driver.FindElement(By.Id("username")).SendKeys(child.UniLogin.Username);

        if (child.UniLogin.AuthType == AuthenticationType.Pictogram)
        {
            // Click pictograms in sequence
            foreach (var pictogram in child.UniLogin.PictogramSequence)
            {
                var element = _driver.FindElement(By.XPath($"//img[@alt='{pictogram}']"));
                element.Click();
                await Task.Delay(500); // Allow UI to update
            }
        }
        else
        {
            // Standard password entry
            _driver.FindElement(By.Id("password")).SendKeys(child.UniLogin.Password);
        }

        // Submit and extract cookies
        _driver.FindElement(By.Id("submit")).Click();
        return ExtractAuthCookies(_driver);
    }
}
```

## Estimated Effort
- **Investigation:** 4 hours (understand exact HTML structure and JavaScript)
- **Implementation:** 8-12 hours (depending on JavaScript complexity)
- **Testing:** 4 hours (various scenarios and edge cases)
- **Total:** 16-20 hours

## Priority Justification
**High Priority** - This feature is essential for families with younger children. Without it, parents must manually fetch week letters for children who use pictogram authentication, defeating the automation purpose.

## Investigation Summary

### What We Learned
1. **Login flow is more complex than expected:** Initial page → Login selector → Unilogin → Username → Pictograms
2. **SAML authentication:** Uses SAML protocol with session codes and signatures
3. **Security challenges:** Server may block automated access (got error page in testing)
4. **Current code gaps:**
   - No handling of login selector page
   - No pictogram detection/selection logic
   - Session management not robust enough for SAML flow

### Recommended Implementation Approach
Given the complexity discovered during testing, **browser automation (Selenium/Playwright) is strongly recommended** over pure HTTP requests because:
- JavaScript execution is likely required for pictogram selection
- Session management through complex redirects is challenging
- Server may have anti-automation measures
- Visual interaction with pictograms is more naturally handled by browser automation

## Next Steps
1. **Decision Required:** Choose between:
   - Pure HTTP approach (complex, may not work)
   - Browser automation (more reliable, adds dependency)
2. **If HTTP approach:** Need to solve session/security issues first
3. **If browser automation:** Add Selenium or Playwright to project
4. **Test with Real Account:** Once approach is chosen, verify with actual pictogram credentials