"""Native MP4/export and static PE checks. This does not emulate older Windows."""
import argparse
import hashlib
import importlib.util
import json
import pathlib
import struct
import subprocess
import zlib
_spec = importlib.util.spec_from_file_location('audit_pe', pathlib.Path(__file__).with_name('audit-pe.py'))
_audit = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_audit)
inspect = _audit.inspect


def png_rgba(width, height):
    def chunk(kind, payload):
        return struct.pack('>I', len(payload)) + kind + payload + struct.pack('>I', zlib.crc32(kind + payload) & 0xffffffff)
    rows = []
    for y in range(height):
        rows.append(b'\0' + b''.join(bytes((255, 0, 0, 255 if 100 <= x < 220 and 50 <= y < 130 else 0)) for x in range(width)))
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(b''.join(rows))) + chunk(b'IEND', b'')


def run(program, args, payload=None):
    result = subprocess.run([str(program), *map(str, args)], input=payload, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=60)
    if result.returncode:
        raise RuntimeError(result.stderr.decode(errors='replace')[-6000:])
    return result.stdout


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', required=True)
    parser.add_argument('--reference-decoder', required=True)
    args = parser.parse_args()
    folder = pathlib.Path(__file__).resolve().parent
    output = pathlib.Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    ffmpeg, ffprobe = folder / 'ffmpeg.exe', folder / 'ffprobe.exe'
    checks = []
    binaries = []
    modern = {'WaitOnAddress', 'WakeByAddressAll', 'WakeByAddressSingle', 'SetThreadDescription', 'GetThreadDescription', 'GetSystemTimePreciseAsFileTime', 'CreateFile2', 'GetTempPath2W', 'DiscardVirtualMemory', 'SetThreadInformation'}
    allowed_dlls = {'kernel32.dll', 'msvcrt.dll', 'advapi32.dll', 'bcrypt.dll', 'user32.dll', 'shell32.dll', 'ole32.dll', 'oleaut32.dll', 'ws2_32.dll', 'secur32.dll', 'psapi.dll', 'gdi32.dll', 'winmm.dll'}
    for program in (ffmpeg, ffprobe):
        pe = inspect(program)
        assert pe['machine'] == '0x8664' and pe['subsystemVersion'] <= [6, 1]
        for imports in (pe['imports'], pe['delayImports']):
            assert all(name.lower() in allowed_dlls for name in imports), imports.keys()
            assert not any(modern.intersection(symbols) for symbols in imports.values())
        binaries.append({'name': program.name, 'size': program.stat().st_size, 'sha256': hashlib.sha256(program.read_bytes()).hexdigest(), 'pe': pe})
        checks.append(program.name + ': x64, PE <=6.1, system DLL allowlist, no known post-Win7 hard imports')
    version = run(ffmpeg, ['-version']).decode(errors='replace')
    assert 'worshipstudio-win7' in version
    assert 'GNU Lesser General Public License' in ' '.join(run(ffmpeg, ['-L']).decode(errors='replace').split())
    checks.append('Native executable starts and reports LGPL build')
    png = png_rgba(320, 180)
    frames = png * 8
    simple = output / 'png-pipe.mp4'
    run(ffmpeg, ['-hide_banner', '-loglevel', 'error', '-nostdin', '-y', '-f', 'image2pipe', '-framerate', 8, '-vcodec', 'png', '-i', 'pipe:0', '-map', '0:v:0', '-c:v', 'mpeg4', '-q:v', 3, '-pix_fmt', 'yuv420p', '-r', 8, '-an', '-t', 1, '-frames:v', 8, '-movflags', '+faststart', simple], frames)
    probe = json.loads(run(ffprobe, ['-v', 'error', '-show_streams', '-show_format', '-of', 'json', simple]))
    video = next(s for s in probe['streams'] if s['codec_type'] == 'video')
    assert video['codec_name'] == 'mpeg4' and video['width'] == 320 and video['height'] == 180 and video['nb_frames'] == '8'
    checks.append('Production PNG pipe -> MP4: MPEG4, 320x180, 8 frames')
    background = output / 'blue-background.mp4'
    audio = output / 'short-tone.wav'
    run(ffmpeg, ['-v', 'error', '-y', '-f', 'lavfi', '-i', 'color=c=blue:s=320x180:r=8:d=0.5', '-c:v', 'mpeg4', background])
    run(ffmpeg, ['-v', 'error', '-y', '-f', 'lavfi', '-i', 'sine=frequency=440:sample_rate=44100:duration=0.25', '-c:a', 'pcm_s16le', audio])
    composite = output / 'overlay-audio.mp4'
    run(ffmpeg, ['-hide_banner', '-loglevel', 'error', '-nostdin', '-y', '-f', 'image2pipe', '-framerate', 8, '-vcodec', 'png', '-i', 'pipe:0', '-stream_loop', -1, '-i', background, '-i', audio, '-filter_complex', '[1:v]scale=320:180:force_original_aspect_ratio=increase,crop=320:180,setsar=1[bg];[bg][0:v]overlay=shortest=1:format=auto[v]', '-map', '[v]', '-c:v', 'mpeg4', '-q:v', 3, '-pix_fmt', 'yuv420p', '-r', 8, '-map', '2:a:0', '-af', 'apad', '-c:a', 'aac', '-b:a', '192k', '-t', 1, '-frames:v', 8, '-movflags', '+faststart', composite], frames)
    streams = json.loads(run(ffprobe, ['-v', 'error', '-show_streams', '-show_format', '-of', 'json', composite]))
    video = next(s for s in streams['streams'] if s['codec_type'] == 'video')
    track = next(s for s in streams['streams'] if s['codec_type'] == 'audio')
    assert video['codec_name'] == 'mpeg4' and video['nb_frames'] == '8' and track['codec_name'] == 'aac'
    assert 0.95 <= float(track['duration']) <= 1.1
    checks.append('Production looping background + PNG alpha overlay + short WAV -> MPEG4/AAC MP4; audio padded to 1 second')
    pixels = run(pathlib.Path(args.reference_decoder), ['-v', 'error', '-i', composite, '-frames:v', 1, '-f', 'rawvideo', '-pix_fmt', 'rgb24', 'pipe:1'])
    assert len(pixels) == 320 * 180 * 3
    at = lambda x, y: pixels[(y * 320 + x) * 3:(y * 320 + x) * 3 + 3]
    center, outside = at(160, 90), at(20, 20)
    assert center[0] > 180 and center[2] < 70 and outside[2] > 180 and outside[0] < 70
    checks.append('Independent decoder verified red foreground and blue background through transparent PNG')
    encoders = run(ffmpeg, ['-hide_banner', '-encoders']).decode(errors='replace')
    assert 'mpeg4' in encoders and 'h264_amf' not in encoders
    checks.append('MPEG4 available; unavailable AMF can fall back to software encoding')
    report = {'checksPassed': len(checks), 'checksFailed': 0, 'checks': checks, 'version': version, 'binaries': binaries, 'physicalWindows7Or8Tested': False, 'limitation': 'Native execution was checked on the available Windows host; import audit does not emulate legacy Windows.'}
    (output / 'ffmpeg-legacy-results.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({'passed': len(checks), 'failed': 0, 'report': str(output / 'ffmpeg-legacy-results.json')}))


if __name__ == '__main__':
    main()
