"""Read normal and delay PE imports without loading or executing the binary."""
import json
import pathlib
import struct
import sys


def inspect(path):
    data = pathlib.Path(path).read_bytes()
    if data[:2] != b'MZ':
        raise ValueError('Not a PE file')
    pe = struct.unpack_from('<I', data, 0x3c)[0]
    section_count = struct.unpack_from('<H', data, pe + 6)[0]
    optional_size = struct.unpack_from('<H', data, pe + 20)[0]
    optional = pe + 24
    is64 = struct.unpack_from('<H', data, optional)[0] == 0x20b
    directories = optional + (112 if is64 else 96)
    image_base = struct.unpack_from('<Q' if is64 else '<I', data, optional + (24 if is64 else 28))[0]
    section_start = optional + optional_size
    sections = [struct.unpack_from('<IIII', data, section_start + index * 40 + 8) for index in range(section_count)]

    def offset(rva):
        for virtual_size, virtual_address, raw_size, raw_position in sections:
            if virtual_address <= rva < virtual_address + max(virtual_size, raw_size):
                return raw_position + rva - virtual_address
        return rva

    def string(position):
        return data[position:data.index(b'\0', position)].decode('ascii', errors='replace')

    def symbols(table):
        position = offset(table)
        step = 8 if is64 else 4
        flag = 1 << (step * 8 - 1)
        result = []
        while True:
            address = struct.unpack_from('<Q' if is64 else '<I', data, position)[0]
            position += step
            if not address:
                return result
            result.append('#' + str(address & 65535) if address & flag else string(offset(address) + 2))

    imports = {}
    rva = struct.unpack_from('<I', data, directories + 8)[0]
    if rva:
        position = offset(rva)
        while True:
            original, timestamp, chain, name, first = struct.unpack_from('<IIIII', data, position)
            if not any((original, timestamp, chain, name, first)):
                break
            imports[string(offset(name))] = symbols(original or first)
            position += 20
    delay_imports = {}
    rva = struct.unpack_from('<I', data, directories + 13 * 8)[0]
    if rva:
        position = offset(rva)
        while True:
            attributes, name, handle, iat, names, bound, unload, timestamp = struct.unpack_from('<IIIIIIII', data, position)
            if not any((attributes, name, handle, iat, names, bound, unload, timestamp)):
                break
            if not attributes & 1:
                name -= image_base
                names -= image_base
            delay_imports[string(offset(name))] = symbols(names)
            position += 32
    return {'path': str(path), 'machine': hex(struct.unpack_from('<H', data, pe + 4)[0]),
            'subsystemVersion': list(struct.unpack_from('<HH', data, optional + 48)),
            'imports': imports, 'delayImports': delay_imports}


if __name__ == '__main__':
    print(json.dumps([inspect(path) for path in sys.argv[1:]], indent=2))
