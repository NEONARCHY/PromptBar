# Publishing PromptBar

Use the contents of this folder as the root of the GitHub repository.

Recommended GitHub repository settings:

- Repository name: `PromptBar`
- Description: `A lightweight floating teleprompter bar for Windows.`
- Visibility: Public
- Add README: Off
- Add .gitignore: No .gitignore
- Add license: No license

README, `.gitignore`, and LICENSE already exist in this folder.

After creating the empty GitHub repository:

```powershell
cd C:\Users\User\Desktop\notchsufler\notchprompt-windows
git init
git add .
git commit -m "Initial PromptBar release"
git branch -M main
git remote add origin https://github.com/NEONARCHY/PromptBar.git
git push -u origin main
```

To create a release with the portable exe:

```powershell
git tag v0.3.0
git push origin v0.3.0
```

GitHub Actions will build and attach `PromptBarPortable.exe` to the release.
