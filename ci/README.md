# فایل CI

`github-workflow-ci.yml` همان workflow گیت‌هاب اکشنز است.

توکن این نشست مجوز `workflows` ندارد، بنابراین نمی‌توان مستقیماً چیزی داخل
`.github/workflows/` پوش کرد. برای فعال کردن CI یک بار به‌صورت محلی اجرا کنید:

```bash
mkdir -p .github/workflows
git mv ci/github-workflow-ci.yml .github/workflows/ci.yml
git commit -m "ci: enable GitHub Actions workflow"
git push
```
