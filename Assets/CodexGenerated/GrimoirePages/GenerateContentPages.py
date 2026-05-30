from PIL import Image, ImageDraw, ImageFont, ImageFilter
from pathlib import Path
import math
import random


OUT = Path(__file__).resolve().parent
FONT_REGULAR = r"C:\WINDOWS\Fonts\malgun.ttf"
FONT_BOLD = r"C:\WINDOWS\Fonts\malgunbd.ttf"
W = H = 1024

FIRE = (224, 72, 34)
ICE = (60, 176, 230)
THUNDER = (225, 190, 38)
INK = (37, 28, 17)
MUTED = (74, 58, 35)
PANEL = (208, 185, 112)

PAGES = [
    {
        "type": "element",
        "title": "Fire / 화염",
        "element": "fire",
        "gesture": "Open Palm",
        "voice": "파이어",
        "effect": "Burn / 지속 피해",
        "usage": [
            "손바닥을 완전 정면보다",
            "살짝 얼굴/카메라 쪽으로 유지",
            "직관적이고 발표용으로 좋음",
        ],
    },
    {
        "type": "element",
        "title": "Ice / 빙결",
        "element": "ice",
        "gesture": "V Sign",
        "voice": "아이스",
        "effect": "Slow / 감속",
        "usage": [
            "검지와 중지를 V자로 펼침",
            "Fist보다 Thumbs Up과 덜 겹침",
            "시각적으로 구분이 쉬움",
        ],
    },
    {
        "type": "element",
        "title": "Thunder / 전격",
        "element": "thunder",
        "gesture": "Finger Gun / Thumbs Up",
        "voice": "썬더",
        "effect": "Stagger / 경직",
        "usage": [
            "Finger Gun은 전기/발사 느낌이 강함",
            "Meta 녹화 방식과 잘 맞을 가능성",
            "Thumbs Up은 대체 제스처로 사용 가능",
        ],
    },
    {
        "type": "list",
        "title": "단일 속성 사용",
        "subtitle": "속성 제스처를 만든 뒤 공격 방향을 정한다.",
        "rows": [
            ("1", "속성 선택", "Fire / Ice / Thunder 제스처 유지"),
            ("2", "시전", "목표를 향해 손을 뻗어 발사"),
            ("3", "상성", "화염 > 빙결 > 전격 > 화염"),
        ],
        "notes": ["단일 속성 마법은 마나 1칸을 소모한다.", "효과는 속성에 따라 Burn / Slow / Stagger."],
    },
    {
        "type": "list",
        "title": "양손 조합 마법",
        "subtitle": "두 속성 제스처를 짧은 시간 안에 맞춘다.",
        "rows": [
            ("Fire + Ice", "Open Palm + V Sign", "화염빙결 조합"),
            ("Ice + Thunder", "V Sign + Finger Gun", "빙결전격 조합"),
            ("Thunder + Fire", "Gun/Thumbs Up + Open Palm", "전격화염 조합"),
        ],
        "notes": ["조합 속성 마법은 마나 2칸을 소모한다.", "두 속성이 모두 해금되어야 사용 가능."],
    },
    {
        "type": "list",
        "title": "배리어 사용법",
        "subtitle": "강한 공격 전에는 회피와 함께 방어를 준비한다.",
        "rows": [
            ("준비", "양손을 몸 앞에 모은다", "시야 중앙에 집중"),
            ("전개", "손바닥을 앞으로 유지", "공격 방향을 막는다"),
            ("해제", "자세를 풀면 장벽 종료", "마나 상황 확인"),
        ],
        "notes": ["세부 입력은 배리어 시스템 연결 후 조정 가능.", "전투 중 손목 마나를 함께 확인한다."],
    },
    {
        "type": "list",
        "title": "마나와 보이스",
        "subtitle": "마나는 4칸, 오른쪽 손목에서 확인한다.",
        "rows": [
            ("마나", "총 4칸", "오른쪽 손목 게이지"),
            ("단일", "1칸 소모", "빠르게 반복 사용"),
            ("조합", "2칸 소모", "높은 대미지와 제어"),
            ("보이스", "선택 사용", "환급 + 대미지 증가"),
        ],
        "notes": ["주문을 외치지 않아도 기본 시전은 가능하다.", "보이스 예시: 파이어 / 아이스 / 썬더."],
    },
    {
        "type": "list",
        "title": "빠른 전투 흐름",
        "subtitle": "제스처, 보이스, 마나를 함께 관리한다.",
        "rows": [
            ("1", "속성 제스처", "왼쪽 페이지의 자세 참고"),
            ("2", "목표 조준", "손 방향으로 시전"),
            ("3", "보이스 강화", "여유가 있으면 주문 외치기"),
            ("4", "상성 교대", "상황에 맞춰 속성 전환"),
        ],
        "notes": ["조합 마법은 강하지만 마나를 빠르게 소모한다.", "마도서는 전투 중 기억 보조용이다."],
    },
]


def font(size, bold=False):
    return ImageFont.truetype(FONT_BOLD if bold else FONT_REGULAR, size)


def text_size(draw, text, text_font):
    box = draw.textbbox((0, 0), text, font=text_font)
    return box[2] - box[0], box[3] - box[1]


def draw_center(draw, xy, text, text_font, fill):
    x, y, w, h = xy
    tw, th = text_size(draw, text, text_font)
    draw.text((x + (w - tw) / 2, y + (h - th) / 2), text, font=text_font, fill=fill)


def wrap_text(draw, text, text_font, max_width):
    lines = []
    for paragraph in text.split("\n"):
        line = ""
        for char in paragraph:
            candidate = line + char
            if text_size(draw, candidate, text_font)[0] <= max_width or not line:
                line = candidate
            else:
                lines.append(line)
                line = char
        if line:
            lines.append(line)
    return lines


def draw_multiline(draw, x, y, text, text_font, fill, max_width, gap=8):
    for line in wrap_text(draw, text, text_font, max_width):
        draw.text((x, y), line, font=text_font, fill=fill)
        y += text_size(draw, line, text_font)[1] + gap
    return y


def page_background(seed):
    random.seed(seed)
    image = Image.new("RGB", (W, H), (190, 169, 100))
    pixels = image.load()
    for y in range(H):
        for x in range(W):
            noise = int(
                18 * math.sin((x + seed * 17) * 0.017)
                + 14 * math.sin((y + seed * 31) * 0.021)
                + random.randint(-5, 5)
            )
            base = 188 + noise
            pixels[x, y] = (
                max(130, min(230, base)),
                max(115, min(210, base - 16)),
                max(70, min(160, base - 66)),
            )
    return image.filter(ImageFilter.GaussianBlur(0.45))


def draw_frame(draw):
    draw.rectangle((42, 42, W - 42, H - 42), outline=INK, width=7)
    draw.rectangle((64, 64, W - 64, H - 64), outline=(92, 70, 38), width=3)


def draw_flame(draw, cx, cy, scale):
    draw.ellipse((cx - 92 * scale, cy - 22 * scale, cx + 92 * scale, cy + 98 * scale), fill=(107, 42, 24), outline=INK, width=max(2, int(4 * scale)))
    outer = [
        (cx, cy - 150 * scale),
        (cx + 78 * scale, cy - 52 * scale),
        (cx + 58 * scale, cy + 70 * scale),
        (cx, cy + 132 * scale),
        (cx - 62 * scale, cy + 70 * scale),
        (cx - 84 * scale, cy - 38 * scale),
    ]
    inner = [
        (cx + 4 * scale, cy - 80 * scale),
        (cx + 44 * scale, cy - 20 * scale),
        (cx + 24 * scale, cy + 66 * scale),
        (cx - 14 * scale, cy + 94 * scale),
        (cx - 34 * scale, cy + 26 * scale),
    ]
    draw.polygon(outer, fill=FIRE, outline=INK)
    draw.polygon(inner, fill=(255, 190, 58), outline=None)


def draw_ice(draw, cx, cy, scale):
    color = ICE
    draw.line((cx - 100 * scale, cy, cx + 100 * scale, cy), fill=color, width=max(5, int(9 * scale)))
    draw.line((cx, cy - 100 * scale, cx, cy + 100 * scale), fill=color, width=max(5, int(9 * scale)))
    draw.line((cx - 74 * scale, cy - 74 * scale, cx + 74 * scale, cy + 74 * scale), fill=color, width=max(4, int(7 * scale)))
    draw.line((cx + 74 * scale, cy - 74 * scale, cx - 74 * scale, cy + 74 * scale), fill=color, width=max(4, int(7 * scale)))
    draw.ellipse((cx - 42 * scale, cy - 42 * scale, cx + 42 * scale, cy + 42 * scale), outline=INK, width=max(2, int(4 * scale)))


def draw_thunder(draw, cx, cy, scale):
    bolt = [
        (cx + 10 * scale, cy - 150 * scale),
        (cx - 72 * scale, cy + 2 * scale),
        (cx - 8 * scale, cy + 2 * scale),
        (cx - 36 * scale, cy + 142 * scale),
        (cx + 82 * scale, cy - 28 * scale),
        (cx + 16 * scale, cy - 28 * scale),
    ]
    draw.polygon(bolt, fill=THUNDER, outline=INK)


def draw_icon(draw, element, cx, cy, scale=1.0):
    if element == "fire":
        draw_flame(draw, cx, cy, scale)
    elif element == "ice":
        draw_ice(draw, cx, cy, scale)
    else:
        draw_thunder(draw, cx, cy, scale)


def make_element_page(index, data):
    image = page_background(index)
    draw = ImageDraw.Draw(image)
    draw_frame(draw)

    accent = {"fire": FIRE, "ice": ICE, "thunder": THUNDER}[data["element"]]
    draw.text((92, 92), data["title"], font=font(58, True), fill=INK)
    draw.line((92, 166, 932, 166), fill=INK, width=4)

    left_box = (98, 222, 392, 680)
    right_box = (442, 222, 904, 820)
    draw.rounded_rectangle(left_box, radius=24, fill=PANEL, outline=(92, 70, 38), width=4)
    draw.rounded_rectangle(right_box, radius=24, fill=PANEL, outline=(92, 70, 38), width=4)

    draw_icon(draw, data["element"], 245, 418, 1.35)
    draw_center(draw, (120, 590, 250, 56), data["gesture"], font(32, True), accent)
    draw_center(draw, (120, 638, 250, 34), "속성 제스처", font(22), MUTED)

    y = 252
    draw.text((480, y), "사용방법", font=font(36, True), fill=INK)
    y += 68
    for line in data["usage"]:
        draw.text((490, y), "- " + line, font=font(27), fill=MUTED)
        y += 50
    y += 18
    draw.text((490, y), "보이스", font=font(30, True), fill=INK)
    draw.text((650, y), data["voice"], font=font(32, True), fill=accent)
    y += 64
    draw.text((490, y), "효과", font=font(30, True), fill=INK)
    draw.text((650, y), data["effect"], font=font(29, True), fill=accent)
    y += 78
    draw_multiline(draw, 490, y, "주문을 외치는 것은 선택이지만 성공하면 마나 환급 및 대미지 증가 효과가 있다.", font(24), INK, 380, gap=8)

    image.save(OUT / f"Page_Content_{index:02d}.png")


def make_list_page(index, data):
    image = page_background(index)
    draw = ImageDraw.Draw(image)
    draw_frame(draw)
    accent = [(20, 205, 128), (142, 73, 230), (230, 82, 42), (42, 160, 220)][index % 4]
    draw_center(draw, (92, 86, 840, 72), data["title"], font(54, True), INK)
    draw_multiline(draw, 132, 178, data["subtitle"], font(28), MUTED, 760, gap=4)

    y = 310
    for label, main, description in data["rows"]:
        draw.rounded_rectangle((94, y, 930, y + 92), radius=18, fill=PANEL, outline=(92, 70, 38), width=3)
        draw.text((124, y + 22), label, font=font(27, True), fill=INK)
        draw.text((348, y + 16), main, font=font(28, True), fill=accent)
        draw.text((348, y + 52), description, font=font(23), fill=MUTED)
        y += 112

    y = 838
    for note in data["notes"][:2]:
        draw.text((120, y), "- " + note, font=font(24), fill=INK)
        y += 38

    image.save(OUT / f"Page_Content_{index:02d}.png")


for page_index, page in enumerate(PAGES, start=1):
    if page["type"] == "element":
        make_element_page(page_index, page)
    else:
        make_list_page(page_index, page)

print(f"created {len(PAGES)} content pages at {OUT}")
