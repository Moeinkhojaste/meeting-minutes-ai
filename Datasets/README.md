# Private evaluation dataset

This directory contains only the versioned dataset contract and generated
fixtures. Real recordings, reference transcripts, annotations, and the local
manifest remain ignored.

- `manifest.schema.v1.json` is the machine-readable contract.
- `manifest.example.v1.json` is synthetic and safe to commit. It demonstrates
  the required five development and three locked final-test entries.
- `manifest.local.json` is the ignored manifest for permitted real recordings.

Copy the example to `manifest.local.json`, replace every generated entry with
verified metadata, and validate it:

```powershell
.\.venv\Scripts\python.exe .\ai-service\dataset_manifest.py `
  .\Datasets\manifest.local.json
```

Do not mark the dataset gate complete until every real recording has documented
consent/rights, an independently checked reference, and any required timestamp
and speaker annotations. Lock the final-test split before model or prompt
tuning.
