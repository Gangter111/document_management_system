import path from 'path';
import fs from 'fs';
// @ts-ignore
import pdf from 'pdf-parse/lib/pdf-parse.js';
import mammoth from 'mammoth';
import { createWorker } from 'tesseract.js';

interface FieldResult {
  value: string | null;
  confidence: number;
  needsReview: boolean;
}

interface ExtractionResult {
  doc_number: FieldResult;
  doc_date: FieldResult;
  summary: FieldResult;
  sender: FieldResult;
  receiver: FieldResult;
  signer: FieldResult;
  department: FieldResult;
  overall_confidence: number;
}

const DEPARTMENTS = [
  'Ban Giám đốc',
  'Phòng Hành chính',
  'Phòng Kế toán',
  'Phòng Kỹ thuật',
  'Phòng Kinh doanh',
  'Văn phòng',
  'Hội đồng quản trị'
];

const PATTERNS = {
  doc_number: [
    /Số:\s*([\d\w\/\-]+)/i,
    /Số hiệu:\s*([\d\w\/\-]+)/i,
    /No\.?:\s*([\d\w\/\-]+)/i,
  ],
  doc_date: [
    /(?:ngày|date)\s+(\d{1,2})\s+(?:tháng|month)\s+(\d{1,2})\s+(?:năm|year)\s+(\d{4})/i,
    /(\d{1,2})[\/\-\.](\d{1,2})[\/\-\.](\d{4})/,
    /(\d{4})[\/\-\.](\d{1,2})[\/\-\.](\d{1,2})/,
  ],
  summary: [
    /(?:V\/v|Trích yếu|Subject):\s*([^\n\r]+)/i,
    /(?:Nội dung|Content):\s*([^\n\r]+)/i,
  ],
  signer: [
    /(?:Ký bởi|Signed by|Người ký):\s*([^\n\r]+)/i,
    /(?:Chức vụ|Position):\s*[^\n\r]+\s+([A-ZÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚĂĐĨŨƠƯÝỲỴỶỸ][a-zàáâãèéêìíòóôõùúăđĩũơưýỳỵỷỹ\s]+)$/m,
  ],
  department: DEPARTMENTS.map(d => new RegExp(d, 'i'))
};

function normalizeText(text: string): string {
  return text
    .replace(/\r\n/g, '\n')
    .replace(/[ \t]+/g, ' ')
    .replace(/\n\s*\n/g, '\n')
    .trim();
}

function segmentDocument(text: string) {
  const lines = text.split('\n');
  const totalLines = lines.length;
  
  // Heuristic: Header is first 20%, Signature is last 20%
  const headerEnd = Math.min(Math.floor(totalLines * 0.25), 15);
  const signatureStart = Math.max(totalLines - Math.min(Math.floor(totalLines * 0.2), 10), headerEnd + 1);

  return {
    header: lines.slice(0, headerEnd).join('\n'),
    body: lines.slice(headerEnd, signatureStart).join('\n'),
    signature: lines.slice(signatureStart).join('\n'),
    full: text
  };
}

export async function extractMetadata(filePath: string): Promise<ExtractionResult> {
  const ext = path.extname(filePath).toLowerCase();
  let text = '';

  try {
    if (ext === '.pdf') {
      const dataBuffer = fs.readFileSync(filePath);
      const data = await pdf(dataBuffer);
      text = data.text;

      if (text.trim().length < 50) {
        text = await performOCR(filePath);
      }
    } else if (ext === '.docx') {
      const result = await mammoth.extractRawText({ path: filePath });
      text = result.value;
    } else if (['.jpg', '.jpeg', '.png'].includes(ext)) {
      text = await performOCR(filePath);
    }
  } catch (error) {
    console.error('Extraction error:', error);
  }

  return runRules(normalizeText(text));
}

async function performOCR(filePath: string): Promise<string> {
  const worker = await createWorker('vie');
  const { data: { text } } = await worker.recognize(filePath);
  await worker.terminate();
  return text;
}

function runRules(text: string): ExtractionResult {
  const segments = segmentDocument(text);
  
  const createField = (value: string | null = null, confidence: number = 0): FieldResult => ({
    value,
    confidence,
    needsReview: confidence < 0.7 || !value
  });

  const result: ExtractionResult = {
    doc_number: createField(),
    doc_date: createField(),
    summary: createField(),
    sender: createField(),
    receiver: createField(),
    signer: createField(),
    department: createField(),
    overall_confidence: 0
  };

  // 1. Doc Number (Header preferred)
  for (const p of PATTERNS.doc_number) {
    const match = segments.header.match(p) || segments.full.match(p);
    if (match) {
      result.doc_number = createField(match[1], 0.95);
      break;
    }
  }

  // 2. Doc Date (Header preferred)
  for (const p of PATTERNS.doc_date) {
    const match = segments.header.match(p) || segments.full.match(p);
    if (match) {
      let dateVal = match[0];
      if (match.length === 4) {
        dateVal = `${match[3]}-${match[2].padStart(2, '0')}-${match[1].padStart(2, '0')}`;
      }
      result.doc_date = createField(dateVal, 0.9);
      break;
    }
  }

  // 3. Summary (Body/Header)
  for (const p of PATTERNS.summary) {
    const match = segments.full.match(p);
    if (match) {
      result.summary = createField(match[1].trim(), 0.85);
      break;
    }
  }

  // 4. Signer (Signature area preferred)
  for (const p of PATTERNS.signer) {
    const match = segments.signature.match(p) || segments.full.match(p);
    if (match) {
      result.signer = createField(match[1].trim(), 0.8);
      break;
    }
  }
  // Fallback for signer: last line of signature if it looks like a name
  if (!result.signer.value && segments.signature) {
    const sigLines = segments.signature.split('\n').filter(l => l.trim().length > 3);
    if (sigLines.length > 0) {
      const lastLine = sigLines[sigLines.length - 1].trim();
      if (/^[A-ZÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚĂĐĨŨƠƯÝỲỴỶỸ][a-zàáâãèéêìíòóôõùúăđĩũơưýỳỵỷỹ\s]+$/.test(lastLine)) {
        result.signer = createField(lastLine, 0.6);
      }
    }
  }

  // 5. Department (Dictionary match)
  for (const p of PATTERNS.department) {
    const match = segments.header.match(p) || segments.full.match(p);
    if (match) {
      result.department = createField(match[0], 0.9);
      break;
    }
  }

  const fields = [result.doc_number, result.doc_date, result.summary, result.signer, result.department];
  const totalConfidence = fields.reduce((acc, f) => acc + f.confidence, 0);
  result.overall_confidence = totalConfidence / fields.length;

  return result;
}
