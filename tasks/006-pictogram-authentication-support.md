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
    // Build form data with selected pictograms
    var formData = new Dictionary<string, string>();
    var form = doc.DocumentNode.SelectSingleNode("//form");

    // Add hidden fields
    var hiddenFields = form.SelectNodes(".//input[@type='hidden']");
    foreach (var field in hiddenFields ?? new HtmlNodeCollection(null))
    {
        formData[field.GetAttributeValue("name", "")] = field.GetAttributeValue("value", "");
    }

    // Find and select pictograms
    var selectedPictograms = new List<string>();
    foreach (var pictogramName in pictogramSequence)
    {
        // Find pictogram element by alt text, data attribute, or name
        var pictogram = doc.DocumentNode.SelectSingleNode(
            $"//img[@alt='{pictogramName}']" +
            $" | //input[@value='{pictogramName}']" +
            $" | //*[@data-pictogram='{pictogramName}']"
        );

        if (pictogram != null)
        {
            var value = pictogram.GetAttributeValue("value", "")
                     ?? pictogram.GetAttributeValue("data-value", "")
                     ?? pictogram.GetAttributeValue("id", "");
            selectedPictograms.Add(value);
        }
    }

    // Add selected pictograms to form data (field name might be 'pictograms[]' or similar)
    formData["selectedPictograms"] = string.Join(",", selectedPictograms);

    // Submit the form with selected pictograms
    var action = form.GetAttributeValue("action", "");
    var response = await _httpClient.PostAsync(action, new FormUrlEncodedContent(formData));

    return response.IsSuccessStatusCode;
}
```

## Challenges & Considerations

### Technical Challenges
1. **Login Selector Page:** Need to add handling for the initial login selector (straightforward)
2. **Session Management:** SAML flow with cookies - already handled by our HttpClient
3. **Pictogram Element Detection:** Need to identify clickable pictogram elements by name/alt/data attributes
4. **Form Submission:** May need to build custom form data based on pictogram selection

### Solutions
1. **Login Selector:** Simple form POST with `selectedIdp=uni_idp`
2. **Pictogram Detection:** Parse HTML for images/buttons with matching identifiers
3. **Selection Simulation:** Build form data with selected pictogram values
4. **Session Handling:** Use existing cookie container in HttpClient

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

## Implementation Code Updates

### Enhanced UniLoginClient to Handle Login Selector

```csharp
protected override async Task<bool> ProcessLoginResponseAsync(string content, string currentUrl)
{
    var doc = new HtmlDocument();
    doc.LoadHtml(content);

    // Check for login selector page
    var loginButtons = doc.DocumentNode.SelectNodes("//button[@name='selectedIdp']");
    if (loginButtons != null)
    {
        // Find and submit Unilogin option
        var form = doc.DocumentNode.SelectSingleNode("//form");
        var action = form?.GetAttributeValue("action", "");

        var formData = new Dictionary<string, string>
        {
            ["selectedIdp"] = "uni_idp"
        };

        var response = await _httpClient.PostAsync(action, new FormUrlEncodedContent(formData));
        content = await response.Content.ReadAsStringAsync();
        doc.LoadHtml(content);
    }

    // Continue with standard username/password or pictogram flow
    return await base.ProcessLoginResponseAsync(content, currentUrl);
}
```

## Estimated Effort
- **Investigation:** ✅ COMPLETE (HTML structure understood, authentication flow mapped)
- **Implementation:** 4-6 hours (straightforward HTML parsing and form submission)
- **Testing:** 2 hours (test with real pictogram credentials)
- **Total:** 6-8 hours

## Priority Justification
**High Priority** - This feature is essential for families with younger children. Without it, parents must manually fetch week letters for children who use pictogram authentication, defeating the automation purpose.

## Implementation Summary

### What We Learned
1. **Login flow structure:** Initial page → Login selector → Unilogin → Username → Pictograms → Success
2. **SAML authentication:** Standard SAML flow with session codes - we handle this already
3. **Pictogram selection is simple:** Just 4 clickable elements with identifiable names (alt text, data attributes, or IDs)
4. **Current code gaps (easily fixable):**
   - Add handling of login selector page (just select "uni_idp" button)
   - Add pictogram detection logic (find images/buttons after username)
   - Add pictogram click simulation (POST with selected pictogram values)

### Implementation Approach - HTTP Only ✅
This is absolutely doable with our current HTTP/HTML parsing approach:
1. **Handle login selector:** Find and click button with `value="uni_idp"`
2. **Submit username:** Standard form submission (already working)
3. **Detect pictogram page:** Look for image grid or pictogram elements
4. **Select pictograms:** Find elements by alt/data attributes matching sequence
5. **Submit selection:** Click "Næste" button or submit form

The pictograms are sanely named (e.g., "image1", "image2", "image3", "image4") and will be identifiable in the HTML. We just need to:
- Parse the pictogram elements
- Build form data with selected pictogram values
- Submit the form

## Next Steps
1. **Enhance UniLoginClient:** Add login selector handling (simple form POST)
2. **Add pictogram detection:** Check for pictogram interface after username
3. **Implement selection logic:** Find and "click" pictograms by matching names
4. **Test with real credentials:** Verify the complete flow works

### Bottom Line
This is a straightforward extension of our existing authentication approach. We're just:
- Handling one extra page (login selector)
- Finding 4 named elements (pictograms)
- Building form data with the selected values
- Submitting the form

No browser automation needed. No JavaScript execution required. Just good old HTTP requests and HTML parsing. 💪