<?php

declare(strict_types=1);

namespace ScheduleLogic\Services;

/**
 * Minimális .xlsx (Office Open XML) író a ClosedXML helyett.
 *
 * Egy munkalapot tud: szöveg/szám cellák, sorkitöltő szín, rögzített első
 * sor, oszlopszélesség a tartalomhoz igazítva. Csak a beépített "zip"
 * kiterjesztés kell hozzá, külső csomag nem.
 */
final class XlsxWriter
{
    /** @var list<array{cells: list<string|int>, fill: ?string}> */
    private array $rows = [];
    /** @var array<string,int> szín (ARGB) => kitöltés-index a styles.xml-ben */
    private array $fills = [];
    private bool $freezeFirstRow = false;

    public function __construct(private readonly string $sheetName = 'Sheet1')
    {
    }

    /** @param list<string|int> $cells */
    public function addRow(array $cells, ?string $fillArgb = null): void
    {
        if ($fillArgb !== null && !isset($this->fills[$fillArgb])) {
            $this->fills[$fillArgb] = count($this->fills) + 2; // 0 és 1 foglalt (none, gray125)
        }
        $this->rows[] = ['cells' => $cells, 'fill' => $fillArgb];
    }

    public function freezeFirstRow(): void
    {
        $this->freezeFirstRow = true;
    }

    public function toBytes(): string
    {
        if (!class_exists(\ZipArchive::class)) {
            throw new \RuntimeException('The PHP "zip" extension is required for Excel export.');
        }
        $tmp = tempnam(sys_get_temp_dir(), 'xlsx');
        $zip = new \ZipArchive();
        if ($zip->open($tmp, \ZipArchive::OVERWRITE) !== true) {
            throw new \RuntimeException('Could not create xlsx archive.');
        }

        $zip->addFromString('[Content_Types].xml', $this->contentTypes());
        $zip->addFromString('_rels/.rels', $this->rootRels());
        $zip->addFromString('xl/workbook.xml', $this->workbook());
        $zip->addFromString('xl/_rels/workbook.xml.rels', $this->workbookRels());
        $zip->addFromString('xl/styles.xml', $this->styles());
        $zip->addFromString('xl/worksheets/sheet1.xml', $this->sheet());
        $zip->close();

        $bytes = (string)file_get_contents($tmp);
        @unlink($tmp);
        return $bytes;
    }

    // ------------------------------------------------------------------

    private static function esc(string $s): string
    {
        // Az XML 1.0-ban tiltott vezérlőkaraktereket eldobjuk.
        $s = preg_replace('/[\x00-\x08\x0B\x0C\x0E-\x1F]/', '', $s);
        return htmlspecialchars($s, ENT_QUOTES | ENT_XML1, 'UTF-8');
    }

    private static function columnName(int $index): string
    {
        $name = '';
        for ($n = $index + 1; $n > 0; $n = intdiv($n - 1, 26)) {
            $name = chr(65 + ($n - 1) % 26) . $name;
        }
        return $name;
    }

    private static function textLength(string $s): int
    {
        return preg_match('//u', $s) ? (int)preg_match_all('/./su', $s) : strlen($s);
    }

    private function sheet(): string
    {
        // AdjustToContents: a leghosszabb cella alapján, kis ráhagyással
        $widths = [];
        foreach ($this->rows as $row) {
            foreach ($row['cells'] as $i => $value) {
                $widths[$i] = max($widths[$i] ?? 0, self::textLength((string)$value));
            }
        }

        $xml = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            . '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
            . 'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">';

        $xml .= '<sheetViews><sheetView tabSelected="1" workbookViewId="0">';
        if ($this->freezeFirstRow) {
            $xml .= '<pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/>'
                . '<selection pane="bottomLeft" activeCell="A2" sqref="A2"/>';
        }
        $xml .= '</sheetView></sheetViews>';
        $xml .= '<sheetFormatPr defaultRowHeight="15"/>';

        if ($widths) {
            $xml .= '<cols>';
            foreach ($widths as $i => $w) {
                $width = min(255, max(8.43, $w * 1.1 + 2));
                $xml .= sprintf('<col min="%d" max="%d" width="%.2f" customWidth="1"/>', $i + 1, $i + 1, $width);
            }
            $xml .= '</cols>';
        }

        $xml .= '<sheetData>';
        foreach ($this->rows as $r => $row) {
            $rowNumber = $r + 1;
            $style = $row['fill'] !== null ? ' s="' . ($this->fills[$row['fill']] - 1) . '"' : '';
            $xml .= '<row r="' . $rowNumber . '">';
            foreach ($row['cells'] as $c => $value) {
                $ref = self::columnName($c) . $rowNumber;
                if (is_int($value)) {
                    $xml .= '<c r="' . $ref . '"' . $style . '><v>' . $value . '</v></c>';
                } else {
                    $xml .= '<c r="' . $ref . '"' . $style . ' t="inlineStr"><is><t xml:space="preserve">'
                        . self::esc((string)$value) . '</t></is></c>';
                }
            }
            $xml .= '</row>';
        }
        $xml .= '</sheetData>';
        $xml .= '<pageMargins left="0.7" right="0.7" top="0.75" bottom="0.75" header="0.3" footer="0.3"/>';
        $xml .= '</worksheet>';
        return $xml;
    }

    private function styles(): string
    {
        $fills = '<fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill>';
        foreach ($this->fills as $argb => $_) {
            $fills .= '<fill><patternFill patternType="solid"><fgColor rgb="' . $argb . '"/><bgColor indexed="64"/></patternFill></fill>';
        }
        // cellXfs: 0 = alap, majd minden kitöltéshez egy stílus
        $xfs = '<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>';
        foreach ($this->fills as $fillIndex) {
            $xfs .= '<xf numFmtId="0" fontId="0" fillId="' . $fillIndex . '" borderId="0" xfId="0" applyFill="1"/>';
        }

        return '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            . '<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
            . '<fonts count="1"><font><sz val="11"/><color theme="1"/><name val="Calibri"/><family val="2"/><scheme val="minor"/></font></fonts>'
            . '<fills count="' . (2 + count($this->fills)) . '">' . $fills . '</fills>'
            . '<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>'
            . '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>'
            . '<cellXfs count="' . (1 + count($this->fills)) . '">' . $xfs . '</cellXfs>'
            . '<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>'
            . '</styleSheet>';
    }

    private function workbook(): string
    {
        return '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            . '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
            . 'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
            . '<sheets><sheet name="' . self::esc(substr($this->sheetName, 0, 31)) . '" sheetId="1" r:id="rId1"/></sheets>'
            . '</workbook>';
    }

    private function workbookRels(): string
    {
        return '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            . '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            . '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>'
            . '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>'
            . '</Relationships>';
    }

    private function rootRels(): string
    {
        return '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            . '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            . '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>'
            . '</Relationships>';
    }

    private function contentTypes(): string
    {
        return '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            . '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
            . '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
            . '<Default Extension="xml" ContentType="application/xml"/>'
            . '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>'
            . '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>'
            . '<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>'
            . '</Types>';
    }
}
