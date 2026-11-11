#!/usr/bin/env python3
"""Isolated synthetic controls. None of these fixtures is native UI evidence."""
import argparse
import copy
import importlib.util
from pathlib import Path
import tempfile
import shutil
import sys
from types import SimpleNamespace
import json
from PIL import Image
import unittest

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('native_evidence', HERE/'verify-native-evidence.py')
v = importlib.util.module_from_spec(spec)
spec.loader.exec_module(v)


def keeper(art, frame, mirrored):
    scale = .0792 if art == v.IDLE else .0688
    return {'id':4,'art':art,'frame':frame,'mirrored':mirrored,
            'scale':[-scale if mirrored else scale,scale], 'position':[500,300], 'supportPlane':[500,300],
            'offset':(v.IDLE_OFFSETS if art == v.IDLE else v.HIT_OFFSETS)[frame]}


def motion(mirrored=False):
    result = []
    for i in range(54):
        elapsed = (i+1)*1000/60
        reaction = elapsed-480
        art, frame = (v.HIT,v.frame_at(reaction,v.HIT_ENDS)) if 0 <= reaction < 320-1e-7 else (v.IDLE,0)
        shot_time = elapsed-160
        source_art,source_frame = (v.SHOT,v.frame_at(shot_time,(100,200,320,400,500,600))) if 0 <= shot_time < 600-1e-7 else (('C01-FFV1-UNIT-ARCHER',1) if shot_time < 0 else ('archer-breathe-v1',0))
        source_x = 600-min(elapsed/160,1)*40
        source = {'id':18,'art':source_art,'frame':source_frame,'position':[source_x,340],
                  'supportPlane':[source_x,340],'scale':[.0893,.0893] if source_art == v.SHOT else [.1,.1],
                  'offset':v.SHOT_OFFSETS[source_frame] if source_art == v.SHOT else [0,0],'mirrored':False}
        result.append({'captureMode':'motion','captureStride':1,'observedFrame':i+1,
            'processFrame':2000+i,'elapsedMs':i*30,'presenting':elapsed < 900-1e-7,
            'actors':[keeper(art,frame,mirrored),source]})
    return result


def save():
    return {'campaign':{'savedAt':'old-campaign','state':{'sentinel':True}},
        'battle':{'savedAt':'old-battle','state':{'units':[
            {'id':4,'type':'c01.gravekeeper','facing':'east','hp':37},
            {'id':18,'type':'c01.archer','facing':'north','hp':39}]}},'events':[]}


def settled(document):
    result = copy.deepcopy(document)
    result['battle']['state']['units'][0]['hp'] = 4
    return result


def fresh(document):
    result = copy.deepcopy(document)
    result['campaign']['savedAt'] = 'new-campaign'
    result['battle']['savedAt'] = 'new-battle'
    return result


class NativeEvidenceControls(unittest.TestCase):
    def rejects(self,change):
        traces = motion(); change(traces)
        with self.assertRaises(ValueError): v.verify_trace('hit-right',traces)

    def test_positive_motion_both_directions(self):
        for mirrored,kind in [(False,'hit-right'),(True,'hit-left')]:
            result = v.verify_trace(kind,motion(mirrored))
            self.assertEqual(result['observations'],54)
            self.assertEqual(result['recoilOnsetObservation'],28)
            self.assertEqual(result['recoveryObservation'],47)

    def test_not_hardcoded_count(self):
        # A detached synthetic leading movement observation is permitted. The
        # observed hit/receipt timeline, not a guessed total count, owns duration.
        traces = motion(); traces.insert(0,copy.deepcopy(traces[0]))
        traces[0]['actors'][1]['position'][0] += 1
        traces[0]['actors'][1]['supportPlane'][0] += 1
        for i,t in enumerate(traces):
            t.update(observedFrame=i+1,processFrame=2000+i,elapsedMs=i*30)
        self.assertEqual(v.verify_trace('hit-right',traces)['observations'],55)

    def test_idle_mode_rejected(self):
        self.rejects(lambda t:t[0].update(captureMode='idle'))

    def test_stride_rejected(self):
        self.rejects(lambda t:t[2].update(captureStride=3))

    def test_duplicate_process_frame_rejected(self):
        self.rejects(lambda t:t[3].update(processFrame=t[2]['processFrame']))

    def test_observation_gap_rejected(self):
        self.rejects(lambda t:t[3].update(observedFrame=9))

    def test_backward_clock_rejected(self):
        self.rejects(lambda t:t[3].update(elapsedMs=-1))

    def test_incomplete_capture_rejected(self):
        with self.assertRaises(ValueError):v.verify_trace('hit-right',motion()[:-1])

    def test_early_receipt_rejected(self):
        self.rejects(lambda t:t[50].update(presenting=False))

    def test_wrong_facing_rejected(self):
        with self.assertRaises(ValueError):v.verify_trace('hit-left',motion())

    def test_registration_rejected(self):
        self.rejects(lambda t:t[30]['actors'][0].update(offset=[-243,-553]))

    def test_scale_rejected(self):
        self.rejects(lambda t:t[30]['actors'][0].update(scale=[.0689,.0689]))

    def test_contact_jump_rejected(self):
        self.rejects(lambda t:t[30]['actors'][0].update(position=[501,300]))

    def test_duplicate_actor_rejected(self):
        self.rejects(lambda t:t[0]['actors'].append(copy.deepcopy(t[0]['actors'][0])))

    def test_membership_rejected(self):
        self.rejects(lambda t:t[20]['actors'].pop())

    def test_reordered_recoil_rejected(self):
        self.rejects(lambda t:t[35]['actors'].__setitem__(0,keeper(v.HIT,3,False)))

    def test_early_recovery_rejected(self):
        self.rejects(lambda t:t[46]['actors'].__setitem__(0,keeper(v.IDLE,0,False)))

    def test_no_source_move_rejected(self):
        def change(t):
            for trace in t:trace['actors'][1]['position']=[560,340]
        self.rejects(change)

    def test_missing_source_pose_rejected(self):
        def change(t):
            for trace in t:
                a=trace['actors'][1]
                if a['art']==v.SHOT and a['frame']==3:a['frame']=4
        self.rejects(change)

    def test_rgb_mutation_rejected(self):
        pixels=b'synthetic fixture, never native evidence'
        v.verify_rgb(pixels,v.sha(pixels))
        with self.assertRaises(ValueError):v.verify_rgb(pixels+b'x',v.sha(pixels))

    def test_identity_mismatch_rejected(self):
        hashes={name:'0'*64 for name in v.ASSEMBLY_NAMES}
        anchor={'sourceCommit':'a','godotSourceTree':'b','toolsSourceTree':'c','assemblySha256':hashes}
        v.verify_identity(['a','b','c'],hashes,anchor)
        with self.assertRaises(ValueError):v.verify_identity(['wrong','b','c'],hashes,anchor)
        with self.assertRaises(ValueError):v.verify_identity(['a','b','c'],{},anchor)

    def test_complete_f5_save_accepted(self):
        original=save(); expected=settled(original)
        self.assertTrue(v.verify_saves('hit-right',original,fresh(expected),original,expected)['freshOfficialSaveTimestamps'])

    def test_copied_input_rejected(self):
        original=save()
        with self.assertRaises(ValueError):v.verify_saves('hit-right',original,original,original,settled(original))

    def test_copied_expected_timestamps_rejected(self):
        original=save(); expected=settled(original)
        with self.assertRaises(ValueError):v.verify_saves('hit-right',original,expected,original,expected)

    def test_legal_input_mutation_rejected(self):
        original=save(); altered=copy.deepcopy(original); altered['events'].append({'type':'bad'})
        with self.assertRaises(ValueError):v.verify_saves('hit-right',altered,fresh(settled(original)),original,settled(original))

    def test_complete_output_mutations_rejected(self):
        original=save(); expected=settled(original)
        for change in ('hp','facing','campaign','events','extra','boolean'):
            altered=fresh(expected)
            if change=='hp':altered['battle']['state']['units'][0]['hp']+=1
            elif change=='facing':altered['battle']['state']['units'][0]['facing']='west'
            elif change=='campaign':altered['campaign']['state']['extra']=True
            elif change=='events':altered['events'].append({'type':'bad'})
            elif change=='extra':altered['unexpected']=True
            else:altered['campaign']['state']['sentinel']=1
            with self.subTest(change=change),self.assertRaises(ValueError):
                v.verify_saves('hit-right',original,altered,original,expected)

    def test_wrong_damage_rejected_even_if_expected_matches(self):
        original=save(); expected=settled(original); expected['battle']['state']['units'][0]['hp']=5
        with self.assertRaises(ValueError):v.verify_saves('hit-right',original,fresh(expected),original,expected)


class ArchiveByteControls(unittest.TestCase):
    def test_original_trace_bytes_accepted(self):
        lines=[b'{"presenting":true}',b'{"presenting":false}']
        records=[{'traceSha256':v.sha(line)} for line in lines]
        self.assertEqual(len(v.verify_trace_bytes(b'\n'.join(lines)+b'\n',records)),2)

    def test_trace_reserialization_rejected(self):
        raw=b'{"presenting":true}'
        with self.assertRaises(ValueError):
            v.verify_trace_bytes(b'{"presenting": true}\n',[{'traceSha256':v.sha(raw)}])

    def test_trace_separator_or_count_mutation_rejected(self):
        raw=b'{"x":1}'; records=[{'traceSha256':v.sha(raw)}]
        for data in (raw,raw+b'\r\n',raw+b'\n\n',raw+b'\n'+raw+b'\n'):
            with self.subTest(data=data),self.assertRaises(ValueError):v.verify_trace_bytes(data,records)

    def test_manifest_all_provenance_mutations_rejected(self):
        with tempfile.TemporaryDirectory(prefix='synthetic-manifest-',dir=HERE) as temporary:
            root=Path(temporary)
            names=['native-input.json','native-output.json','native-source.txt','native-assemblies-sha256.txt',
                   'native.log','native.exit','native-proof.json','native-lossless.mp4','native-observation-000.png',
                   'native-keeper-poses-3x.png','README.md']
            for name in names:(root/name).write_bytes(b'synthetic fixture, not native evidence')
            (root/'artifact-sha256.txt').write_text(''.join(f'{v.sha((root/name).read_bytes())}  {name}\n' for name in names))
            v.verify_manifest(root)
            for name in names:
                original=(root/name).read_bytes(); (root/name).write_bytes(original+b'tamper')
                with self.subTest(name=name),self.assertRaises(ValueError):v.verify_manifest(root)
                (root/name).write_bytes(original)

    def test_unlisted_file_rejected(self):
        with tempfile.TemporaryDirectory(prefix='synthetic-manifest-',dir=HERE) as temporary:
            root=Path(temporary); (root/'artifact-sha256.txt').write_text(''); (root/'extra').write_text('x')
            with self.assertRaises(ValueError):v.verify_manifest(root)

    def test_path_escape_rejected(self):
        for name in ('../outside','/tmp/outside','a/b','..'):
            with self.subTest(name=name),self.assertRaises(ValueError):v.archive_path(HERE,name)


class LosslessVideoControls(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        spec=importlib.util.spec_from_file_location('native_packager',HERE/'package-native.py')
        cls.p=importlib.util.module_from_spec(spec); spec.loader.exec_module(cls.p)
        cls.temporary=tempfile.TemporaryDirectory(prefix='synthetic-video-',dir=HERE)
        cls.root=Path(cls.temporary.name); cls.source=cls.root/'raw'; cls.source.mkdir()
        cls.output=cls.root/'archive'; cls.output.mkdir()
        cls.files=[]
        for i in range(2):
            path=cls.source/f'frame-{i:03d}.png'
            image=Image.new('RGBA',v.VIEWPORT,(i*70,30,100,255)); image.putpixel((12,12),(255,80,30,128)); image.save(path)
            path.with_suffix('.json').write_text(json.dumps({'synthetic':True,'index':i}))
            cls.files.append(path)
        cls.video,cls.frames,_=cls.p.encode_and_compare(cls.source,cls.files,cls.output,60)
        cls.video_bytes=cls.video.read_bytes()
        cls.still=cls.output/'native-observation-000.png'; cls.still.write_bytes(cls.files[0].read_bytes())
        cls.actors=[keeper(v.IDLE,0,False),keeper(v.HIT,0,False)]
        cls.sheet=cls.p.diagnostic_sheet(cls.output,cls.files,cls.actors,'native-keeper-poses-3x.png',[0,1],[0,0,20,20],2)
        cls.proof={'videoSha256':v.sha(cls.video_bytes),'stills':{cls.still.name:0},'diagnosticSheets':[cls.sheet]}

    @classmethod
    def tearDownClass(cls):
        cls.temporary.cleanup()

    def test_roundtrip_all_pixels_stills_crops(self):
        v.verify_video(self.video,self.frames,60,self.proof)

    def test_raw_rgb_hash_mutation_rejected(self):
        frames=copy.deepcopy(self.frames); frames[1]['rgbSha256']='0'*64
        with self.assertRaises(ValueError):v.verify_video(self.video,frames,60,self.proof)

    def test_video_container_mutation_rejected(self):
        self.video.write_bytes(self.video_bytes+b'tamper')
        try:
            with self.assertRaises(ValueError):v.verify_video(self.video,self.frames,60,self.proof)
        finally:self.video.write_bytes(self.video_bytes)

    def test_still_mutation_rejected(self):
        original=self.still.read_bytes(); self.still.write_bytes(original+b'tamper')
        try:
            with self.assertRaises(ValueError):v.verify_video(self.video,self.frames,60,self.proof)
        finally:self.still.write_bytes(original)

    def test_crop_pixel_mutation_rejected(self):
        path=self.output/self.sheet['file']; original=path.read_bytes()
        with Image.open(path) as image:
            image.putpixel((0,32),(1,2,3)); image.save(path)
        try:
            with self.assertRaises(ValueError):v.verify_video(self.video,self.frames,60,self.proof)
        finally:path.write_bytes(original)

    def test_crop_geometry_mutation_rejected(self):
        proof=copy.deepcopy(self.proof); proof['diagnosticSheets'][0]['crop']=[1,0,21,20]
        with self.assertRaises(ValueError):v.verify_video(self.video,self.frames,60,proof)

    def test_non_three_times_crop_rejected(self):
        proof=copy.deepcopy(self.proof); proof['diagnosticSheets'][0]['scale']=2
        with self.assertRaises(ValueError):v.verify_video(self.video,self.frames,60,proof)


def archive_controls(args):
    archive=args.archive.resolve()
    identity=args.identity or HERE/'frozen-identity.json'
    def parameters(root):
        return SimpleNamespace(kind=args.kind,identity=identity,capture_dir=None,proof=root/'native-proof.json',
            traces=root/'native-frames.jsonl',video=root/'native-lossless.mp4',native_exit=None,session_log=None,
            source=None,hashes=None,input_save=root/'native-input.json',output_save=root/'native-output.json',legal_root=args.legal_root)
    v.verify(parameters(archive))
    checks=[]
    with tempfile.TemporaryDirectory(prefix='deliberate-tamper-controls-',dir=HERE) as temporary:
        root=Path(temporary)/'detached-copy'
        def reset():
            if root.exists():shutil.rmtree(root)
            shutil.copytree(archive,root)
        def manifest():
            (root/'artifact-sha256.txt').write_text(''.join(f'{v.sha(p.read_bytes())}  {p.name}\n'
                for p in sorted(root.iterdir()) if p.is_file() and p.name!='artifact-sha256.txt'))
        def rejected(label):
            try:v.verify(parameters(root))
            except (ValueError,KeyError,json.JSONDecodeError) as error:
                checks.append({'control':label,'rejected':True,'reason':str(error)})
            else:raise AssertionError(f'Deliberate tamper accepted: {label}')
        proof=v.load(archive/'native-proof.json')
        names=['native-input.json','native-output.json','native-source.txt','native-assemblies-sha256.txt',
               'native.log','native.exit','native-frames.jsonl','native-lossless.mp4','native-proof.json',
               next(iter(proof['stills'])),proof['diagnosticSheets'][0]['file'],'native-startup.png','save-comparison.json']
        for name in names:
            reset(); path=root/name; path.write_bytes(path.read_bytes()+b'\nDELIBERATE TAMPER CONTROL\n')
            rejected('byte mutation: '+name)
        for mode in ('keeper-pose','output-hp','source-identity','log-error','exit-failure','crop-pixels'):
            reset(); changed=v.load(root/'native-proof.json')
            if mode=='keeper-pose':
                path=root/'native-frames.jsonl'; lines=path.read_bytes()[:-1].split(b'\n')
                index=changed['recoilOnsetObservation']; trace=json.loads(lines[index])
                next(a for a in trace['actors'] if a['id']==4)['frame']=2
                lines[index]=json.dumps(trace,separators=(',',':')).encode()
                path.write_bytes(b'\n'.join(lines)+b'\n'); changed['frames'][index]['traceSha256']=v.sha(lines[index]); changed['traceSha256']=v.sha(path.read_bytes())
            elif mode=='output-hp':
                path=root/'native-output.json'; doc=v.load(path); next(u for u in doc['battle']['state']['units'] if u['id']==4)['hp']=5
                path.write_text(json.dumps(doc)); changed['outputSaveSha256']=v.sha(path.read_bytes())
                changed['sourceFileSha256'][path.name]=changed['originalSourceFileSha256'][path.name]=v.sha(path.read_bytes())
            elif mode=='source-identity':changed['sourceCommit']='0'*40
            elif mode=='log-error':
                path=root/'native.log'; path.write_bytes(path.read_bytes()+b'ERROR: deliberate control\n')
                changed['nativeSessionSha256']=v.sha(path.read_bytes())
                changed['sourceFileSha256'][path.name]=changed['originalSourceFileSha256'][path.name]=v.sha(path.read_bytes())
            elif mode=='exit-failure':
                path=root/'native.exit';path.write_text('1\n');changed['nativeExitSha256']=v.sha(path.read_bytes())
                changed['sourceFileSha256'][path.name]=changed['originalSourceFileSha256'][path.name]=v.sha(path.read_bytes())
            else:
                sheet=changed['diagnosticSheets'][0];path=root/sheet['file']
                with Image.open(path) as image:
                    image.putpixel((0,sheet['labelHeight']),(1,2,3));image.save(path)
                sheet['sha256']=v.sha(path.read_bytes())
            (root/'native-proof.json').write_text(json.dumps(changed,indent=2)+'\n');manifest()
            rejected('semantic mutation with recalculated archive hashes: '+mode)
    return {'passed':True,'kind':args.kind,'baselineArchiveVerified':True,'syntheticControlsAreNotNativeEvidence':True,
            'mutationsRejected':checks,'count':len(checks)}


if __name__=='__main__':
    parser=argparse.ArgumentParser(add_help=False)
    parser.add_argument('--archive',type=Path)
    parser.add_argument('--kind',choices=v.KINDS)
    parser.add_argument('--legal-root',type=Path)
    parser.add_argument('--identity',type=Path)
    parser.add_argument('--report',type=Path)
    args,unittest_args=parser.parse_known_args()
    if args.archive:
        if not args.kind or not args.legal_root:parser.error('--archive requires --kind and --legal-root')
        report=archive_controls(args)
        if args.report:
            with args.report.open('x') as output:json.dump(report,output,indent=2);output.write('\n')
        print(f"PASS: {report['kind']}: {report['count']} deliberate actual-archive mutations rejected.")
    unittest.main(argv=[sys.argv[0],*unittest_args])
