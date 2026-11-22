#!/bin/sh
# Run with w64devkit 2.11.0 sh; sources are kept unmodified.
# Usage: sh build.sh FFMPEG_SOURCE ZLIB_SOURCE BUILD_DIRECTORY DESTINATION
set -eu
task_ffmpeg_source=$1
task_zlib_source=$2
task_build_directory=$3
task_destination=$4
mkdir -p "$task_build_directory/deps/include" "$task_build_directory/deps/lib" "$task_build_directory/objects" "$task_destination"
make -C "$task_zlib_source" -f win32/Makefile.gcc -j8 libz.a
cp "$task_zlib_source/libz.a" "$task_build_directory/deps/lib/libz.a"
cp "$task_zlib_source/zlib.h" "$task_zlib_source/zconf.h" "$task_build_directory/deps/include/"
cd "$task_build_directory/objects"
sh "$task_ffmpeg_source/configure" \
  --target-os=mingw32 --arch=x86_64 --cc=gcc --cxx=g++ \
  --disable-autodetect --disable-everything --disable-network \
  --disable-doc --disable-debug --disable-asm --disable-shared --enable-static \
  --disable-pthreads --enable-w32threads --enable-zlib \
  --enable-ffmpeg --enable-ffprobe --disable-ffplay \
  --enable-protocol=file,pipe \
  --enable-demuxer=image2pipe,image2,png_pipe,mov,matroska,wav,mp3,aac,flac,ogg,concat \
  --enable-decoder=png,mjpeg,bmp,mpeg4,h264,hevc,aac,mp3,mp3float,pcm_s16le,pcm_s24le,pcm_s32le,pcm_f32le,flac,vorbis,opus,rawvideo,wrapped_avframe \
  --enable-parser=png,mpegaudio,aac,mpeg4video,h264,hevc \
  --enable-encoder=mpeg4,aac,pcm_s16le,png,rawvideo,wrapped_avframe \
  --enable-muxer=mp4,mov,null,image2,wav \
  --enable-bsf=aac_adtstoasc,h264_mp4toannexb,hevc_mp4toannexb,null \
  --enable-filter=apad,aresample,crop,format,scale,setsar,overlay,null,anull,aformat,color,testsrc,sine,fps \
  --enable-indev=lavfi \
  --extra-cflags="-D_WIN32_WINNT=0x0601 -I$task_build_directory/deps/include" \
  --extra-ldflags="-static -L$task_build_directory/deps/lib -Wl,--major-subsystem-version,6,--minor-subsystem-version,1" \
  --extra-version=worshipstudio-win7
make V=1 -j8 ffmpeg.exe ffprobe.exe
cp ffmpeg.exe ffprobe.exe "$task_destination/"
