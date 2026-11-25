import base64, concurrent.futures, copy, html, http.cookiejar, json, re, struct, subprocess, sys, threading, urllib.error, urllib.parse, urllib.request, uuid, zlib
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit('Usage: regression-checks.py <isolated session.json>; run after Velora.Verify test')
ROOT = Path(sys.argv[1]).resolve().parent
FIXTURE = json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
assert re.fullmatch(r'IdentityEmailVeloraChecks_[a-f0-9]{32}', FIXTURE['Database'])
BASE = FIXTURE['BaseUrl']
assert re.fullmatch(r'http://127\.0\.0\.1:\d+', BASE)
RESULTS = []

def sql(query):
    run = subprocess.run(['sqlcmd', '-S', r'(localdb)\MSSQLLocalDB', '-d', FIXTURE['Database'], '-E', '-C', '-b', '-h', '-1', '-W', '-w', '65535', '-Q', 'SET NOCOUNT ON; ' + query], capture_output=True)
    if run.returncode: raise RuntimeError(run.stderr.decode(errors='replace'))
    return run.stdout.decode('utf-8', errors='replace').strip()

def quote(value): return "N'" + value.replace("'", "''") + "'"

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl): return None

class Client:
    def __init__(self, source=None):
        self.jar = http.cookiejar.CookieJar()
        if source:
            for cookie in source.jar: self.jar.set_cookie(copy.copy(cookie))
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar), NoRedirect())
    def request(self, path, data=None, content_type=None):
        request = urllib.request.Request(BASE + path, data=data, headers={'Content-Type': content_type} if content_type else {})
        try: response = self.opener.open(request, timeout=30)
        except urllib.error.HTTPError as error: response = error
        body = response.read().decode('utf-8', errors='replace')
        return response.code, response.headers, body
    def token(self, path):
        code, headers, body = self.request(path)
        assert code == 200, (path, code)
        match = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', body)
        assert match, path
        return html.unescape(match.group(1))
    def post(self, token_page, path, fields):
        fields = {**fields, '__RequestVerificationToken': self.token(token_page)}
        return self.request(path, urllib.parse.urlencode(fields).encode(), 'application/x-www-form-urlencoded')
    def login(self, name, password):
        code, headers, body = self.post('/Login/SignIn', '/Login/SignIn', {'UserName': 'velora.test.'+name, 'Password': password})
        assert code == 302, ('login', name, code)

def check(name, passed, evidence):
    RESULTS.append({'name': name, 'passed': bool(passed), 'evidence': evidence})
    print(('PASS ' if passed else 'FAIL ') + name, flush=True)

alice, bob = Client(), Client()
alice.login('alice', FIXTURE['Password'])
bob.login('bob', FIXTURE['Password'] + 'New!')
prefix = 'Audit-' + uuid.uuid4().hex[:8]

def send(sender, recipient, subject, body):
    code, headers, response = sender.post('/Message/CreateMessage', '/Message/CreateMessage', {'ReceiverEmail': recipient, 'Subject': subject, 'MessageDetail': body})
    assert code == 302, ('send', code)
    message_id = sql('SELECT MessageId FROM Messages WHERE Subject = '+quote(subject))
    assert re.fullmatch(r'[a-f0-9-]{36}', message_id)
    return message_id

# Remaining happy-path requirements omitted by the original checker.
message_id = send(alice, 'bob@velora.test', prefix+'-management', 'Independent participant state')
alice.post('/Message/Inbox', '/Message/ToggleStar', {'id': message_id})
alice.post('/Message/Inbox', '/Message/ToggleStar', {'id': message_id})
check('Star removal persists', sql('SELECT CAST(IsStarred AS int) FROM MessageUserStates WHERE MessageId='+quote(message_id)+' AND UserId='+quote(FIXTURE['Users']['alice'])) == '0', 'Two sequential toggles return to not-starred')
alice.post('/Message/Inbox', '/Message/SetCategory', {'id':message_id, 'categoryId':'1'})
alice.post('/Message/Inbox', '/Message/SetCategory', {'id':message_id, 'categoryId':''})
check('Personal category can be removed', sql('SELECT COUNT(*) FROM MessageUserStates WHERE MessageId='+quote(message_id)+' AND CategoryId IS NULL') == '1', 'Empty category resets only the acting user state')
alice.post('/Message/Sendbox', '/Message/Delete', {'id':message_id})
check('Sender deletion preserves recipient inbox', prefix+'-management' in html.unescape(bob.request('/Message/Inbox')[2]), 'Reverse participant direction also remains isolated')
alice.post('/Message/Trash', '/Message/Restore', {'id':message_id})
code, headers, body = alice.post('/Message/CreateMessage', '/Draft/Save', {'Subject':prefix+'-draft', 'MessageDetail':'First draft'})
draft_id = sql('SELECT DraftMessageId FROM DraftMessages WHERE Subject='+quote(prefix+'-draft'))
code, headers, body = alice.post('/Draft/Edit/'+draft_id, '/Draft/Save', {'DraftMessageId':draft_id, 'Subject':prefix+'-edited', 'MessageDetail':'Edited draft', 'ReceiverEmail':'bob@velora.test'})
check('Editing a draft updates the same record', code == 302 and sql('SELECT COUNT(*) FROM DraftMessages WHERE DraftMessageId='+quote(draft_id)+' AND Subject='+quote(prefix+'-edited')) == '1', 'Existing draft identifier retained')
invalid = alice.post('/Message/CreateMessage', '/Message/CreateMessage', {'ReceiverEmail':'bob@velora.test', 'Subject':'   ', 'MessageDetail':'body'})
check('Whitespace-only subject is rejected', invalid[0] == 200 and 'konu' in html.unescape(invalid[2]).lower(), 'Server validation applies before persistence')

# Full-size valid messages must remain forwardable without losing content.
long_id = send(alice, 'bob@velora.test', prefix+'-long', 'A'*20000)
code, headers, forward_html = alice.request('/Message/CreateMessage?forwardId='+long_id)
forward_body = html.unescape(re.search(r'<textarea[^>]*name="MessageDetail"[^>]*>(.*?)</textarea>', forward_html, re.S).group(1))
# HTML parsers discard the first newline immediately following a textarea tag.
forward_body = re.sub(r'^\r?\n', '', forward_body, count=1).replace('\r\n','\n')
forward_subject = prefix+'-forwarded'
response = alice.post('/Message/CreateMessage?forwardId='+long_id, '/Message/CreateMessage', {'ReceiverEmail':'bob@velora.test', 'Subject':forward_subject, 'MessageDetail':forward_body})
check('Maximum-length message can be forwarded unchanged', response[0] == 302, {'original_length':20000, 'composer_length':len(forward_body), 'post_status':response[0], 'new_messages':sql('SELECT COUNT(*) FROM Messages WHERE Subject='+quote(forward_subject))})

check('Forward preserves every original character', forward_body == 'A'*20000 and sql('SELECT LEN(MessageDetail) FROM Messages WHERE Subject='+quote(forward_subject)) == '20000', 'No truncation or automatic body growth')
reply_html = bob.request('/Message/CreateMessage?replyTo='+long_id)[2]
reply_body = html.unescape(re.search(r'<textarea[^>]*name="MessageDetail"[^>]*>(.*?)</textarea>', reply_html, re.S).group(1))
reply_body = re.sub(r'^\r?\n', '', reply_body, count=1).replace('\r\n','\n')
reply = bob.post('/Message/CreateMessage', '/Message/CreateMessage', {'ReceiverEmail':'alice@velora.test','Subject':prefix+'-reply','MessageDetail':reply_body})
check('Maximum-length reply remains sendable', reply[0] == 302 and reply_body == 'A'*20000, {'post_status':reply[0], 'length':len(reply_body)})

# Trash read operations remain recipient-only and exclude permanent removal.
trash_id = send(alice, 'bob@velora.test', prefix+'-trash-unread', 'Unread until opened')
bob.post('/Message/Inbox', '/Message/Delete', {'id':trash_id})
trash_detail = bob.request('/Message/MessageDetail/'+trash_id)
read_response = bob.post('/Message/Trash', '/Message/SetRead', {'id':trash_id, 'isRead':'true'})
check('Recipient can mark a trash detail read', read_response[0] in (200,302) and 'data-auto-read' in trash_detail[2] and sql('SELECT CAST(IsRead AS int) FROM Messages WHERE MessageId='+quote(trash_id)) == '1', {'detail_status':trash_detail[0], 'automatic_read_form_present':'data-auto-read' in trash_detail[2], 'read_post_status':read_response[0]})
unread_response = bob.post('/Message/Trash','/Message/SetRead',{'id':trash_id,'isRead':'false'})
check('Trash detail can be marked unread', unread_response[0] == 302 and sql('SELECT CAST(IsRead AS int) FROM Messages WHERE MessageId='+quote(trash_id)) == '0' and 'Okunmadı olarak işaretle' in html.unescape(trash_detail[2]), 'Recipient control and SQL state agree')
denied = alice.post('/Message/Sendbox','/Message/SetRead',{'id':trash_id,'isRead':'true'})
check('Sender cannot change recipient read state', denied[0] == 404, {'status':denied[0]})
bob.post('/Message/Trash','/Message/PermanentlyDelete',{'id':trash_id})
denied = bob.post('/Message/Trash','/Message/SetRead',{'id':trash_id,'isRead':'true'})
check('Permanently removed message cannot be marked read', denied[0] == 404, {'status':denied[0]})

# PNG signature alone is not sufficient to establish a decodable image.
token = alice.token('/Profile/Index')
boundary = 'audit'+uuid.uuid4().hex
parts = []
for key, value in {'Name':'Ada', 'Surname':'Test', '__RequestVerificationToken':token}.items():
    parts.append(('--'+boundary+'\r\nContent-Disposition: form-data; name="'+key+'"\r\n\r\n'+value+'\r\n').encode())
invalid_png = b'\x89PNG\r\n\x1a\nnot-a-decodable-image'
parts.append(('--'+boundary+'\r\nContent-Disposition: form-data; name="Photo"; filename="broken.png"\r\nContent-Type: image/png\r\n\r\n').encode()+invalid_png+b'\r\n')
parts.append(('--'+boundary+'--\r\n').encode())
photo_response = alice.request('/Profile/Index', b''.join(parts), 'multipart/form-data; boundary='+boundary)
check('Corrupt PNG payload is rejected', photo_response[0] == 200, {'payload_bytes':len(invalid_png), 'post_status':photo_response[0]})

def photo_upload(payload, mime='image/png'):
    boundary = 'regression'+uuid.uuid4().hex
    fields = {'Name':'Ada','Surname':'Test','__RequestVerificationToken':alice.token('/Profile/Index')}
    chunks = [('--'+boundary+'\r\nContent-Disposition: form-data; name="'+key+'"\r\n\r\n'+value+'\r\n').encode() for key,value in fields.items()]
    chunks.append(('--'+boundary+'\r\nContent-Disposition: form-data; name="Photo"; filename="photo"\r\nContent-Type: '+mime+'\r\n\r\n').encode()+payload+b'\r\n')
    chunks.append(('--'+boundary+'--\r\n').encode())
    return alice.request('/Profile/Index', b''.join(chunks), 'multipart/form-data; boundary='+boundary)

def png_chunk(kind, data):
    return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
def make_png(width,height):
    pixels = zlib.compress((b'\0'+b'\x10\x20\x30'*width)*height)
    return b'\x89PNG\r\n\x1a\n'+png_chunk(b'IHDR',struct.pack('>IIBBBBB',width,height,8,2,0,0,0))+png_chunk(b'IDAT',pixels)+png_chunk(b'IEND',b'')

valid_png = make_png(2,2)
accepted = photo_upload(valid_png)
avatar = sql('SELECT ImageUrl FROM AspNetUsers WHERE Id='+quote(FIXTURE['Users']['alice']))
check('Decodable PNG is accepted and served', accepted[0] == 302 and avatar.endswith('.png') and alice.request(avatar)[0] == 200, {'status':accepted[0]})
for name,payload,mime in [('truncated PNG',valid_png[:33],'image/png'),('oversized dimensions',make_png(4097,1),'image/png'),('corrupt JPEG',b'\xff\xd8\xff\xe0not-a-jpeg','image/jpeg')]:
    before = sql('SELECT ImageUrl FROM AspNetUsers WHERE Id='+quote(FIXTURE['Users']['alice']))
    rejected = photo_upload(payload,mime)
    after = sql('SELECT ImageUrl FROM AspNetUsers WHERE Id='+quote(FIXTURE['Users']['alice']))
    check(name+' rejected without changing avatar', rejected[0] == 200 and before == after, {'status':rejected[0]})

forged_png = valid_png[:33]+struct.pack('>I',0x7fffffff)+b'IDAT'+valid_png[-12:]
rejected = photo_upload(forged_png)
check('Forged PNG allocation length is rejected', rejected[0] == 200, {'status':rejected[0]})

# A real JPEG fixture generated locally, separate from application decoding.
jpeg = (Path(__file__).resolve().parent/'fixtures'/'profile.jpg').read_bytes()
accepted = photo_upload(jpeg,'image/jpeg')
avatar = sql('SELECT ImageUrl FROM AspNetUsers WHERE Id='+quote(FIXTURE['Users']['alice']))
check('Decodable JPEG is accepted and served', accepted[0] == 302 and avatar.endswith('.jpg') and alice.request(avatar)[0] == 200, {'status':accepted[0]})

# Independent form submissions can race when the personal state row is absent.
race_statuses, race_states = [], []
for round_index in range(3):
    race_id = send(alice, 'bob@velora.test', prefix+'-race-'+str(round_index), 'Concurrent star clicks')
    clients = [Client(alice) for _ in range(4)]
    tokens = [client.token('/Message/Sendbox') for client in clients]
    barrier = threading.Barrier(4)
    def toggle(index):
        barrier.wait(timeout=10)
        data=urllib.parse.urlencode({'id':race_id,'__RequestVerificationToken':tokens[index]}).encode()
        return clients[index].request('/Message/ToggleStar',data,'application/x-www-form-urlencoded')[0]
    for phase in ('first','existing'):
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as executor: race_statuses.extend(executor.map(toggle, range(4)))
        race_states.append(sql('SELECT CAST(IsStarred AS int) FROM MessageUserStates WHERE MessageId='+quote(race_id)+' AND UserId='+quote(FIXTURE['Users']['alice'])))
check('Concurrent first and existing star operations avoid errors and lost toggles', all(status == 302 for status in race_statuses) and race_states == ['0']*6, {'http_statuses':race_statuses, 'even_toggle_states':race_states})

category_statuses, category_counts = [], []
for round_index in range(3):
    race_id = send(alice,'bob@velora.test',prefix+'-category-race-'+str(round_index),'Concurrent category changes')
    clients = [Client(alice) for _ in range(4)]
    tokens = [client.token('/Message/Sendbox') for client in clients]
    barrier = threading.Barrier(4)
    def categorize(index):
        barrier.wait(timeout=10)
        data = urllib.parse.urlencode({'id':race_id,'categoryId':str(index+1),'__RequestVerificationToken':tokens[index]}).encode()
        return clients[index].request('/Message/SetCategory',data,'application/x-www-form-urlencoded')[0]
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as executor: category_statuses.extend(executor.map(categorize,range(4)))
    category_counts.append(sql('SELECT COUNT(*) FROM MessageUserStates WHERE MessageId='+quote(race_id)+' AND UserId='+quote(FIXTURE['Users']['alice'])+' AND CategoryId BETWEEN 1 AND 4'))
check('Concurrent first category operations retain exactly one personal state', all(status == 302 for status in category_statuses) and category_counts == ['1']*3, {'http_statuses':category_statuses,'row_counts':category_counts})

# The preserved layout controller should not expose an executable broken view.
legacy = alice.request('/MessageLayout/Index')
check('Preserved layout route redirects to inbox', legacy[0] == 302 and legacy[1].get('Location') == '/Message/Inbox', {'status':legacy[0], 'location':legacy[1].get('Location')})

(ROOT/'edge-results.json').write_text(json.dumps({'passed':sum(x['passed'] for x in RESULTS),'failed':sum(not x['passed'] for x in RESULTS),'checks':RESULTS},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'passed':sum(x['passed'] for x in RESULTS),'failed':sum(not x['passed'] for x in RESULTS)},ensure_ascii=False))
sys.exit(1 if any(not x['passed'] for x in RESULTS) else 0)
