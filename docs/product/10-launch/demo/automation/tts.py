import asyncio, json, subprocess, edge_tts
async def main():
    sc=json.load(open('narration.json'))
    for s in sc:
        c=edge_tts.Communicate(s['text'],'en-US-AvaNeural',rate='-4%')
        await c.save(f"{s['id']}.mp3")
        d=subprocess.check_output(['ffprobe','-v','error','-show_entries','format=duration','-of','csv=p=0',f"{s['id']}.mp3"]).decode().strip()
        s['dur']=float(d); print(s['id'],s['dur'])
    json.dump(sc,open('narration.json','w'),indent=1)
asyncio.run(main())
