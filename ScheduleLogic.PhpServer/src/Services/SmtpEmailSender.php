<?php

declare(strict_types=1);

namespace ScheduleLogic\Services;

use ScheduleLogic\Core\Config;

/**
 * SMTP levélküldés (a System.Net.Mail.SmtpClient megfelelője, EnableSsl = true).
 *
 *  - 587-es port: sima kapcsolat, majd STARTTLS (mint a .NET SmtpClient)
 *  - 465-ös port: azonnali TLS (implicit SSL)
 *  - hitelesítés: AUTH LOGIN (vagy PLAIN, ha csak az elérhető)
 *
 * Saját, kis implementáció, hogy ne kelljen külső csomag (PHPMailer).
 */
final class SmtpEmailSender
{
    private const TIMEOUT = 30;

    /** @var resource|null */
    private $socket = null;

    public function __construct(private readonly Config $config)
    {
    }

    /** @throws \RuntimeException bármilyen SMTP hiba esetén */
    public function send(string $to, string $subject, string $htmlBody): void
    {
        $host = $this->config->string('MailSend:Host');
        $port = $this->config->int('MailSend:Port', 587);
        $user = $this->config->string('MailSend:User');
        $password = $this->config->string('MailSend:Password');
        $from = $this->config->string('MailSend:From', 'noreply@schedulelogic.hu');

        if ($host === '') {
            throw new \RuntimeException('MailSend:Host is not configured.');
        }
        if (preg_match('/[\r\n]/', $to . $from . $subject)) {
            throw new \InvalidArgumentException('Header injection attempt.');
        }

        $implicitTls = $port === 465;
        $remote = ($implicitTls ? 'ssl://' : 'tcp://') . $host . ':' . $port;
        // Tanúsítvány-ellenőrzés: élesben mindig maradjon bekapcsolva; csak
        // helyi teszt SMTP szerverhez (pl. Mailpit) érdemes kikapcsolni.
        $verify = $this->config->bool('MailSend:VerifyCertificate', true);
        $context = stream_context_create(['ssl' => [
            'verify_peer' => $verify,
            'verify_peer_name' => $verify,
            'allow_self_signed' => !$verify,
            'peer_name' => $host,
        ]]);

        $socket = @stream_socket_client($remote, $errno, $errstr, self::TIMEOUT, STREAM_CLIENT_CONNECT, $context);
        if ($socket === false) {
            throw new \RuntimeException("SMTP connect to {$remote} failed: {$errstr} ({$errno})");
        }
        $this->socket = $socket;
        stream_set_timeout($socket, self::TIMEOUT);

        try {
            $this->expect(220);
            $ehlo = $this->hello();

            if (!$implicitTls) {
                if (!preg_match('/^STARTTLS/mi', $ehlo)) {
                    throw new \RuntimeException('SMTP server does not support STARTTLS.');
                }
                $this->command('STARTTLS', 220);
                $ok = stream_socket_enable_crypto(
                    $socket,
                    true,
                    STREAM_CRYPTO_METHOD_TLSv1_2_CLIENT | STREAM_CRYPTO_METHOD_TLSv1_3_CLIENT
                );
                if ($ok !== true) {
                    throw new \RuntimeException('STARTTLS negotiation failed.');
                }
                $ehlo = $this->hello();
            }

            if ($user !== '') {
                if (preg_match('/^AUTH[ =].*\bLOGIN\b/mi', $ehlo)) {
                    $this->command('AUTH LOGIN', 334);
                    $this->command(base64_encode($user), 334);
                    $this->command(base64_encode($password), 235);
                } else {
                    $this->command('AUTH PLAIN ' . base64_encode("\0{$user}\0{$password}"), 235);
                }
            }

            $this->command('MAIL FROM:<' . $from . '>', 250);
            $this->command('RCPT TO:<' . $to . '>', [250, 251]);
            $this->command('DATA', 354);
            $this->write($this->buildMessage($from, $to, $subject, $htmlBody) . "\r\n.");
            $this->expect(250);
            $this->command('QUIT', 221);
        } finally {
            fclose($socket);
            $this->socket = null;
        }
    }

    private function buildMessage(string $from, string $to, string $subject, string $html): string
    {
        $domain = substr(strrchr($from, '@') ?: '@localhost', 1);
        $headers = [
            'Date: ' . date(DATE_RFC2822),
            'From: ' . $from,
            'To: ' . $to,
            'Subject: =?UTF-8?B?' . base64_encode($subject) . '?=',
            'Message-ID: <' . bin2hex(random_bytes(16)) . '@' . $domain . '>',
            'MIME-Version: 1.0',
            'Content-Type: text/html; charset=utf-8',
            'Content-Transfer-Encoding: base64',
        ];
        // base64 törzsben nem fordulhat elő sor eleji ".", így dot-stuffing nem kell
        return implode("\r\n", $headers) . "\r\n\r\n" . rtrim(chunk_split(base64_encode($html), 76, "\r\n"));
    }

    private function hello(): string
    {
        $name = gethostname() ?: 'localhost';
        return $this->command('EHLO ' . $name, 250);
    }

    /** @param int|list<int> $expected */
    private function command(string $line, int|array $expected): string
    {
        $this->write($line);
        return $this->expect($expected);
    }

    private function write(string $line): void
    {
        if (fwrite($this->socket, $line . "\r\n") === false) {
            throw new \RuntimeException('SMTP write failed.');
        }
    }

    /** @param int|list<int> $expected */
    private function expect(int|array $expected): string
    {
        $response = '';
        while (($line = fgets($this->socket, 1024)) !== false) {
            $response .= $line;
            // többsoros válasz: "250-..." folytatódik, "250 ..." az utolsó sor
            if (strlen($line) < 4 || $line[3] !== '-') {
                break;
            }
        }
        $code = (int)substr($response, 0, 3);
        if (!in_array($code, (array)$expected, true)) {
            $meta = stream_get_meta_data($this->socket);
            $reason = $meta['timed_out'] ? 'timeout' : trim($response);
            throw new \RuntimeException("Unexpected SMTP response: {$reason}");
        }
        // a "250-" előtagok nélkül adjuk vissza a képességek listáját
        return preg_replace('/^\d{3}[ -]/m', '', $response);
    }
}
