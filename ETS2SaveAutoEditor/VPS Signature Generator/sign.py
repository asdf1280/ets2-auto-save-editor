import base64
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding

def sign_utf8(message: str, pem_path: str = "private.pem") -> str:
    data = message.encode("utf-8")
    with open(pem_path, "rb") as f:
        priv = serialization.load_pem_private_key(f.read(), password=None)
    sig = priv.sign(data, padding.PKCS1v15(), hashes.SHA256())
    return base64.b64encode(sig).decode()

if __name__ == "__main__":
    name = input("Write the name: ").strip()
    hwid = input("Write the 8-char HWID hash: ").strip()
    data = f"{name}\n{hwid}"
    open("VPS.flag", "w").write(f"{name}\n{sign_utf8(data)}")
