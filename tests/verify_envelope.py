"""Independent format verifier for a synthetic fixture produced by SecurityTests.
Requires cryptography only in the test environment; the Windows app has no dependency.
"""
import hashlib
import hmac
import json
import struct
import sys
from pathlib import Path

from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives.padding import PKCS7

raw = Path(sys.argv[1]).read_bytes()
assert raw[:8] == b'TDMVLT01'
iterations, = struct.unpack('<I', raw[8:12])
length, = struct.unpack('<I', raw[60:64])
assert iterations == 600000 and length == len(raw) - 96
key = hashlib.pbkdf2_hmac('sha256', b'teste-somente-v1-senha-longa', raw[12:44], iterations, 64)
assert hmac.compare_digest(hmac.new(key[32:], raw[:-32], hashlib.sha256).digest(), raw[-32:])
decryptor = Cipher(algorithms.AES(key[:32]), modes.CBC(raw[44:60])).decryptor()
plain = decryptor.update(raw[64:-32]) + decryptor.finalize()
unpadder = PKCS7(128).unpadder()
data = json.loads(unpadder.update(plain) + unpadder.finalize())
assert data['Credentials'][0]['Password'] == 'SENHA-QUE-NAO-PODE-VAZAR-123!'
assert data['Clients'][0]['Name'] == 'Cliente EXCLUSIVO-SECRETO'
print('PASS independent PBKDF2-SHA256 / HMAC-SHA256 / AES-256-CBC format verification')
