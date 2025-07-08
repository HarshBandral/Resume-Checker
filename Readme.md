# Resume Analyzer ASP.NET Core App

## 1. How to Run the Project

1. Make sure you have [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) installed.
2. Clone this repository or copy the project files to your machine.
3. Open a terminal in the project root directory.
4. Run the following command to restore dependencies:
   ```bash
   dotnet restore
   ```
5. Build and run the project:
   ```bash
   dotnet run
   ```
6. The app will be available at `https://localhost:5001` or `http://localhost:5000` by default.

## 2. How to Set Up the Gemini (OpenAI) API Key

1. Obtain your Gemini API key from Google AI Studio or your provider.
2. Open `appsettings.json` or `appsettings.Development.json` in the project root.
3. Add your API key like this:
   ```json
   {
     "Gemini": "YOUR_GEMINI_API_KEY"
   }
   ```
4. Save the file. The app will use this key to call the Gemini API for resume analysis.

## 3. Example Prompt + Response

**Prompt sent to Gemini:**
```
You are a resume assistant. Review the following resume and extract the following in short bullet form.
1. Name
2. Key Skills (max 5)
3. Strengths (max 3)
4. Weaknesses (max 2)
5. Suggestions (max 2)
6. Suggested Roles (max 3)
...
Resume:
John Doe\nExperienced software engineer with expertise in C#, .NET, and cloud technologies...etc.
```

**Example AI Response:**
```
Name: John Doe

Skills Summary:
- C#
- .NET
- Cloud Computing
- Azure
- REST APIs

Strengths:
- Strong problem-solving skills
- Excellent team player
- Quick learner

Weaknesses:
- Sometimes overly detail-oriented
- Limited experience with frontend frameworks

Suggestions:
- Gain more experience in frontend development
- Contribute to open source projects

Suggested Roles:
- Backend Developer
- Cloud Solutions Architect
- DevOps Engineer
```

## 4. How to Test the App

### Manual Testing
- Open the app in your browser.
- Go to the Resume Upload page.
- Try uploading a PDF, DOCX, or TXT resume, or use the "Paste Text" option.
- Review the AI-generated feedback.
- Save or download the feedback as DOCX.

### Swagger (API) Testing
- If Swagger is enabled, navigate to `/swagger` (e.g., `https://localhost:5001/swagger`).
- Use the Swagger UI to test API endpoints (if exposed).
- You can POST a resume file or text to the appropriate endpoint and view the response.

---

For any issues, please check your API key, file format, or review the error messages shown in the app.
