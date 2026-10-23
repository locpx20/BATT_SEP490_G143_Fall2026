const express = require('express');
const cors = require('cors');
const fs = require('fs');
const path = require('path');
const { Zalo, ThreadType, LoginQRCallbackEventType } = require('zca-js');
const qrcodeTerminal = require('qrcode-terminal');

const app = express();
const PORT = 5050;

app.use(cors());
app.use(express.json());

const sessionPath = path.join(__dirname, 'session.json');
const qrPath = path.join(__dirname, 'qr.png');

let api = null;
let currentQrData = null;
let isLoggingIn = false;
let loginError = null;

const zalo = new Zalo({
    apiType: 'prod',
    apiVersion: 'v2'
});

async function startZalo() {
    // 1. Kiểm tra session cũ
    if (fs.existsSync(sessionPath)) {
        try {
            console.log('🔄 Đang đăng nhập lại bằng session đã lưu...');
            const sessionData = JSON.parse(fs.readFileSync(sessionPath, 'utf8'));
            api = await zalo.login(sessionData);
            console.log('✅ Zalo Bot đăng nhập thành công bằng session cũ!');
            return;
        } catch (err) {
            console.log('⚠️ Session cũ hết hạn hoặc không hợp lệ, tạo mã QR mới...');
            if (fs.existsSync(sessionPath)) fs.unlinkSync(sessionPath);
        }
    }

    // 2. Đăng nhập bằng mã QR
    if (isLoggingIn) return;
    isLoggingIn = true;
    loginError = null;

    try {
        console.log('📱 Đang khởi tạo mã QR đăng nhập Zalo...');
        api = await zalo.loginQR({ qrPath }, async (event) => {
            if (event.type === LoginQRCallbackEventType.QRCodeGenerated) {
                console.log('📷 Đã tạo mã QR Zalo thành công!');
                currentQrData = event.data;
                try {
                    await event.actions.saveToFile(qrPath);
                } catch (e) {}

                // In mã QR ra terminal
                if (event.data && event.data.code) {
                    try {
                        qrcodeTerminal.generate(event.data.code, { small: true });
                    } catch (e) {}
                }
                console.log('👉 Bạn có thể mở trình duyệt: http://localhost:5050 để quét mã QR');
            } else if (event.type === LoginQRCallbackEventType.QRCodeScanned) {
                console.log('📲 Đã quét mã QR, vui lòng bấm "Đăng nhập" trên điện thoại!');
            } else if (event.type === LoginQRCallbackEventType.GotLoginInfo) {
                console.log('💾 Đã nhận thông tin đăng nhập, đang lưu session...');
                fs.writeFileSync(sessionPath, JSON.stringify(event.data, null, 2));
            } else if (event.type === LoginQRCallbackEventType.QRCodeExpired) {
                console.log('⏳ Mã QR hết hạn, đang tạo lại...');
            } else if (event.type === LoginQRCallbackEventType.QRCodeDeclined) {
                console.log('❌ Bạn đã từ chối đăng nhập trên điện thoại.');
                loginError = 'Bạn đã từ chối đăng nhập trên điện thoại.';
            }
        });

        isLoggingIn = false;
        console.log('🎉 Zalo Bot đã sẵn sàng nhận lệnh gửi OTP!');
    } catch (err) {
        isLoggingIn = false;
        loginError = err.message;
        console.error('❌ Lỗi đăng nhập Zalo QR:', err.message);
    }
}

// Endpoint kiểm tra trạng thái
app.get('/status', (req, res) => {
    res.json({
        loggedIn: !!api,
        hasQr: fs.existsSync(qrPath),
        error: loginError
    });
});

// Endpoint lấy ảnh mã QR
app.get('/qr', (req, res) => {
    if (fs.existsSync(qrPath)) {
        res.sendFile(qrPath);
    } else {
        res.status(404).send('Mã QR chưa sẵn sàng');
    }
});

// Endpoint gửi mã OTP tới Zalo qua số điện thoại
app.post('/send-otp', async (req, res) => {
    const { phone, otp, purpose } = req.body;

    if (!phone || !otp) {
        return res.status(400).json({ success: false, message: 'Thiếu số điện thoại hoặc mã OTP' });
    }

    if (!api) {
        return res.status(503).json({
            success: false,
            message: 'Zalo Bot chưa đăng nhập. Vui lòng mở http://localhost:5050 để quét mã QR Zalo trước.'
        });
    }

    try {
        console.log(`🔍 Đang tìm tài khoản Zalo cho số điện thoại: ${phone}...`);
        const user = await api.findUser(phone);

        if (!user || (!user.userId && !user.uid)) {
            console.log(`⚠️ Không tìm thấy tài khoản Zalo nào với số: ${phone}`);
            return res.status(404).json({
                success: false,
                message: `Không tìm thấy tài khoản Zalo nào với số điện thoại ${phone}. Người nhận cần đăng ký tài khoản Zalo bằng số này.`
            });
        }

        const targetUserId = user.userId || user.uid;
        console.log(`👤 Tìm thấy Zalo User ID: ${targetUserId} (${user.displayName || user.zaloName || 'Zalo User'})`);

        const messageText = `🏭 [BATT PPMS] Mã xác thực OTP của bạn là: ${otp}\n(Mục đích: ${purpose || 'Xác thực tài khoản'})\nMã có hiệu lực trong 5 phút. Vui lòng không chia sẻ mã này.`;

        console.log(`💬 Đang gửi tin nhắn Zalo tới ${targetUserId}...`);
        await api.sendMessage(messageText, targetUserId, ThreadType.User);
        console.log(`✅ Đã gửi mã OTP thành công tới Zalo số ${phone}!`);

        return res.json({
            success: true,
            message: `Đã gửi mã OTP ${otp} trực tiếp vào ứng dụng Zalo của số ${phone}!`
        });
    } catch (err) {
        console.error('❌ Lỗi gửi tin nhắn Zalo:', err);
        return res.status(500).json({
            success: false,
            message: `Lỗi gửi tin qua Zalo Bot: ${err.message}`
        });
    }
});

// Trang giao diện web quản lý và quét mã QR
app.get('/', (req, res) => {
    res.send(`
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="utf-8">
    <title>Zalo OTP Bot - BATT PPMS</title>
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0f172a; color: #f8fafc; display: flex; justify-content: center; align-items: center; min-height: 100vh; margin: 0; }
        .card { background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.12); border-radius: 16px; padding: 32px; max-width: 440px; text-align: center; box-shadow: 0 20px 40px rgba(0,0,0,0.5); }
        h1 { font-size: 20px; margin-bottom: 8px; color: #0068ff; }
        p { font-size: 13px; color: #94a3b8; margin-bottom: 20px; }
        .qr-box { background: #fff; padding: 16px; border-radius: 12px; display: inline-block; margin-bottom: 16px; min-width: 200px; min-height: 200px; }
        .qr-img { width: 220px; height: 220px; display: block; }
        .badge { display: inline-block; padding: 6px 14px; border-radius: 999px; font-size: 13px; font-weight: 600; margin-bottom: 16px; }
        .badge-success { background: rgba(16,185,129,0.2); color: #34d399; border: 1px solid #10b981; }
        .badge-wait { background: rgba(234,179,8,0.2); color: #fde047; border: 1px solid #eab308; }
        .btn { background: #0068ff; color: #fff; border: none; padding: 10px 18px; border-radius: 8px; font-weight: 600; cursor: pointer; text-decoration: none; display: inline-block; font-size: 13px; margin: 4px; }
        .btn:hover { background: #0050cc; }
        .test-box { margin-top: 24px; padding-top: 20px; border-top: 1px solid rgba(255,255,255,0.1); }
        input { width: 80%; padding: 9px 12px; border-radius: 6px; border: 1px solid #334155; background: #1e293b; color: #fff; margin-bottom: 10px; font-size: 13px; text-align: center; }
    </style>
</head>
<body>
    <div class="card">
        <h1>💬 BATT PPMS - Zalo OTP Bot</h1>
        <p>Cổng chuyển tiếp tin nhắn OTP trực tiếp vào app Zalo trên điện thoại</p>
        
        <div id="status-area">
            <div class="badge badge-wait">Đang kiểm tra trạng thái...</div>
        </div>

        <div id="qr-container" style="display:none;">
            <div class="qr-box">
                <img id="qr-image" class="qr-img" src="/qr?t=${Date.now()}" alt="Quét mã Zalo" />
            </div>
            <p style="font-size: 12.5px; color: #cbd5e1;">Mở ứng dụng <strong>Zalo trên điện thoại</strong> &gt; Chọn biểu tượng <strong>Quét mã QR</strong> để đăng nhập Bot</p>
            <button class="btn" onclick="location.reload()">🔄 Tải lại trang</button>
        </div>

        <div id="ready-container" style="display:none;">
            <div class="badge badge-success">✓ Zalo Bot đã sẵn sàng gửi OTP!</div>
            <p style="color: #6ee7b7;">Bây giờ khi bạn thao tác trên web BATT PPMS, tin nhắn OTP sẽ gửi thẳng vào app Zalo trên điện thoại của bạn.</p>
            
            <div class="test-box">
                <p><strong>Thử nghiệm gửi tin nhắn Zalo:</strong></p>
                <input type="text" id="test-phone" placeholder="Nhập số điện thoại Zalo (vd: 0964208057)" value="0964208057" />
                <br>
                <button class="btn" onclick="sendTestOtp()">📲 Gửi thử 1 tin nhắn OTP</button>
                <div id="test-result" style="margin-top: 10px; font-size: 12.5px;"></div>
            </div>
        </div>
    </div>

    <script>
        async function checkStatus() {
            try {
                const res = await fetch('/status');
                const data = await res.json();
                const statusArea = document.getElementById('status-area');
                const qrContainer = document.getElementById('qr-container');
                const readyContainer = document.getElementById('ready-container');

                if (data.loggedIn) {
                    statusArea.style.display = 'none';
                    qrContainer.style.display = 'none';
                    readyContainer.style.display = 'block';
                } else {
                    statusArea.innerHTML = '<div class="badge badge-wait">Vui lòng quét mã QR dưới đây bằng Zalo</div>';
                    qrContainer.style.display = 'block';
                    readyContainer.style.display = 'none';
                    document.getElementById('qr-image').src = '/qr?t=' + Date.now();
                }
            } catch (e) {
                console.error(e);
            }
        }

        async function sendTestOtp() {
            const phone = document.getElementById('test-phone').value;
            const resDiv = document.getElementById('test-result');
            resDiv.innerHTML = '⏳ Đang gửi tin nhắn qua Zalo...';
            try {
                const res = await fetch('/send-otp', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ phone, otp: Math.floor(100000 + Math.random() * 900000).toString(), purpose: 'Thử nghiệm hệ thống' })
                });
                const data = await res.json();
                if (data.success) {
                    resDiv.innerHTML = '<span style="color:#34d399;">✓ ' + data.message + '</span>';
                } else {
                    resDiv.innerHTML = '<span style="color:#f87171;">✕ ' + data.message + '</span>';
                }
            } catch (e) {
                resDiv.innerHTML = '<span style="color:#f87171;">✕ Lỗi: ' + e.message + '</span>';
            }
        }

        checkStatus();
        setInterval(checkStatus, 3000);
    </script>
</body>
</html>
    `);
});

app.listen(PORT, () => {
    console.log(`=======================================================`);
    console.log(`🚀 Zalo OTP Bot Service đang chạy tại: http://localhost:${PORT}`);
    console.log(`=======================================================`);
    startZalo();
});
