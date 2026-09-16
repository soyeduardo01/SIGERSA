#!/usr/bin/env python3
"""Concatena el informe principal con evidencias privadas alojadas en Supabase.

El manifiesto es un arreglo JSON con objetos que contienen: name, mimeType,
bucketName y supabasePath. Requiere SUPABASE_URL y SUPABASE_SERVICE_ROLE_KEY.
"""

from __future__ import annotations

import argparse
import io
import json
import os
import urllib.parse
import urllib.request
from pathlib import Path

from PIL import Image, ImageOps
from pypdf import PdfReader, PdfWriter


def download(url: str, service_key: str, bucket: str, path: str) -> bytes:
    location = "/".join(urllib.parse.quote(part, safe="") for part in (bucket, *path.split("/")))
    request = urllib.request.Request(
        f"{url.rstrip('/')}/storage/v1/object/authenticated/{location}",
        headers={"Authorization": f"Bearer {service_key}", "apikey": service_key},
    )
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def image_pdf(content: bytes) -> io.BytesIO:
    image = Image.open(io.BytesIO(content))
    image = ImageOps.exif_transpose(image).convert("RGB")
    target = io.BytesIO()
    image.save(target, "PDF", resolution=150.0)
    target.seek(0)
    return target


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--main", required=True, type=Path, help="PDF principal generado por SIGERSA")
    parser.add_argument("--manifest", required=True, type=Path, help="Manifiesto JSON de evidencias")
    parser.add_argument("--output", required=True, type=Path, help="PDF final")
    args = parser.parse_args()

    supabase_url = os.environ.get("SUPABASE_URL", "")
    service_key = os.environ.get("SUPABASE_SERVICE_ROLE_KEY", "")
    if not supabase_url or not service_key:
        raise SystemExit("Configure SUPABASE_URL y SUPABASE_SERVICE_ROLE_KEY.")

    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    writer = PdfWriter()
    writer.append(str(args.main))
    for evidence in manifest:
        content = download(
            supabase_url,
            service_key,
            evidence["bucketName"],
            evidence["supabasePath"],
        )
        mime_type = evidence["mimeType"].lower()
        if mime_type == "application/pdf":
            writer.append(PdfReader(io.BytesIO(content)))
        elif mime_type in {"image/jpeg", "image/png"}:
            writer.append(PdfReader(image_pdf(content)))
        else:
            raise ValueError(f"Tipo de evidencia no admitido: {mime_type}")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("wb") as stream:
        writer.write(stream)


if __name__ == "__main__":
    main()
