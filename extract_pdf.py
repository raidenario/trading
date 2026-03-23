import sys
import subprocess
try:
    import pypdf
except ImportError:
    subprocess.check_call([sys.executable, "-m", "pip", "install", "pypdf"])
    import pypdf

reader = pypdf.PdfReader(sys.argv[1])
text = ""
for page in reader.pages:
    text += page.extract_text() + "\n"

with open(sys.argv[2], "w", encoding="utf-8") as f:
    f.write(text)
