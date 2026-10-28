import json, subprocess, glob
m = json.load(open('marks.json'))['marks']; ids = list(m); t0 = m['intro'] - 0.5
inp = ['-ss', f'{t0:.2f}', '-i', glob.glob('vid2/*.webm')[0]]
for i in ids: inp += ['-i', f'{i}.mp3']
d = lambda i: int((m[i] - t0 + 0.3) * 1000)
fc = ''.join(f"[{k+1}:a]adelay={d(i)}|{d(i)}[a{k}];" for k, i in enumerate(ids))
fc += ''.join(f'[a{k}]' for k in range(len(ids))) + f'amix=inputs={len(ids)}:normalize=0,loudnorm=I=-16:TP=-1.5[aout]'
subprocess.check_call(['ffmpeg', '-y', '-loglevel', 'error', *inp, '-filter_complex', fc,
    '-map', '0:v', '-map', '[aout]', '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '20', '-r', '30',
    '-c:a', 'aac', '-b:a', '160k', '-shortest', 'orders-api-scalar-demo-v2.mp4'])
