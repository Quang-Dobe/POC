import { test, expect } from '@playwright/test';
import { execSync } from 'node:child_process';

const IMAGE = 'poc-backend';

test.describe('E2E-5: backend fails fast when the secret store is unreachable', () => {
  test('standalone container with an unreachable Vault exits non-zero and reports a store failure', () => {
    test.setTimeout(120_000);

    const command =
      `docker run --rm ` +
      `-e ENV=DEV ` +
      `-e BAO_TOKEN=dev-only-token ` +
      `-e Vault__Address=http://127.0.0.1:9999 ` +
      `${IMAGE} 2>&1`;

    let exitCode = 0;
    let output = '';
    try {

      output = execSync(command, { encoding: 'utf8', timeout: 90_000 });
      exitCode = 0;
    } catch (err) {

      const e = err as { status?: number | null; stdout?: Buffer | string; stderr?: Buffer | string };
      exitCode = typeof e.status === 'number' ? e.status : 1;
      output = `${bufToStr(e.stdout)}${bufToStr(e.stderr)}`;
    }

    expect(exitCode, `backend should exit non-zero when the store is unreachable; output:\n${output}`)
      .not.toBe(0);

    const lower = output.toLowerCase();
    expect(lower).toContain('secret store');
    expect(lower).toContain('unreachable');

    expect(output).not.toContain('Now listening on');
  });
});

function bufToStr(value: Buffer | string | undefined): string {
  if (value === undefined) {
    return '';
  }
  return typeof value === 'string' ? value : value.toString('utf8');
}
