#!/usr/bin/env python
# -*- coding: utf-8 -*-

import os
import shutil
from dataclasses import dataclass
from pickletools import uint4, uint8
import pandas as pd
import lz4.block
import sys
import struct


@dataclass
class BlobHeader:
    magic: uint4 = None
    version: uint4 = None
    local_entry_cnt: uint4 = None
    global_entry_cnt: uint4 = None
    store_id: uint4 = None


@dataclass
class DllDescriptor:
    data_offset: uint4 = None
    data_size: uint4 = None
    debug_data_offset: uint4 = None
    debug_data_size: uint4 = None
    config_data_offset: uint4 = None
    config_data_size: uint4 = None
    name: str = None


@dataclass
class HashDescriptor:
    hash: uint8 = None
    mapping_index: uint4 = None
    local_store_index: uint4 = None
    store_id: uint4 = None


dll_descriptors = []
hash32_descriptors = []
hash64_descriptors = []


def print_usage_and_exit():
    sys.exit(" usage: ./command <target-apk-directory>")


def extract_dlls(assemblies_dir_path):
    assemblies_path = assemblies_dir_path + '/assemblies.blob'
    manifest_path = assemblies_dir_path + '/assemblies.manifest'

    if not os.path.exists(assemblies_dir_path):
        return False
    if not os.path.exists(assemblies_path):
        return False
    else:
        blob_magic = b'XABA'
        blob_header = BlobHeader()

        with open(assemblies_path, "rb") as assemblies_file:
            data = assemblies_file.read()

            if data[:4] != blob_magic:
                sys.exit(" > The input file does not contain the expected magic(XABA ... ) bytes.")

            blob_header.version = struct.unpack('<I', data[4:8])[0]
            blob_header.local_entry_cnt = struct.unpack('<I', data[8:12])[0]
            blob_header.global_entry_cnt = struct.unpack('<I', data[12:16])[0]
            blob_header.store_id = struct.unpack('<I', data[16:20])[0]
            print(' > \'%s\' has %d dlls.' % (assemblies_path, blob_header.local_entry_cnt))

            index = 20
            for i in range(blob_header.local_entry_cnt):
                dll_descriptor = DllDescriptor()
                dll_descriptor.data_offset = struct.unpack('<I', data[index:index + 4])[0]
                dll_descriptor.data_size = struct.unpack('<I', data[index + 4:index + 8])[0]
                dll_descriptor.debug_data_offset = struct.unpack('<I', data[index + 8:index + 12])[0]
                dll_descriptor.debug_data_size = struct.unpack('<I', data[index + 12:index + 16])[0]
                dll_descriptor.config_data_offset = struct.unpack('<I', data[index + 16:index + 20])[0]
                dll_descriptor.config_data_size = struct.unpack('<I', data[index + 20:index + 24])[0]
                index += 24
                dll_descriptors.append(dll_descriptor)

            print(' > parsing \'%s\' to %d dlls...' % (assemblies_path, blob_header.local_entry_cnt))

            for i in range(blob_header.local_entry_cnt):
                dll_path = assemblies_dir_path + '/%d.dll' % (i)
                dll_offset = dll_descriptors[i].data_offset
                dll_size = dll_descriptors[i].data_size
                with open(dll_path, "wb") as dll_file:
                    dll_file.write(data[dll_offset:dll_offset + dll_size])
                    dll_file.close()
            """
            for i in range(blob_header.local_entry_cnt):
                hash32_descriptor = HashDescriptor()
                hash32_descriptor.hash = struct.unpack('<Q', data[index:index+8])[0]
                hash32_descriptor.mapping_index = struct.unpack('<I', data[index+8:index+12])[0]
                hash32_descriptor.local_store_index = struct.unpack('<I', data[index+12:index+16])[0]
                hash32_descriptor.store_id = struct.unpack('<I', data[index+16:index+20])[0]
                hash32_descriptors.append(hash32_descriptor)
                index += 20

            for i in range(blob_header.local_entry_cnt):
                hash64_descriptor = HashDescriptor()
                hash64_descriptor.hash = struct.unpack('<Q', data[index:index+8])[0]
                hash64_descriptor.mapping_index = struct.unpack('<I', data[index+8:index+12])[0]
                hash64_descriptor.local_store_index = struct.unpack('<I', data[index+12:index+16])[0]
                hash64_descriptor.store_id = struct.unpack('<I', data[index+16:index+20])[0]
                hash64_descriptors.append(hash32_descriptor)
                index += 20
            """

            df = pd.read_csv(manifest_path, encoding='utf-8', sep=' ', skipinitialspace=True)
            df = df.drop(columns=['ID', 'Blob.1', 'idx', 'Name'])
            df.columns = ['Hash32', 'Hash64', 'BlobID', 'BlobIdx', 'Name']

            names = df['Name']
            not_to_name = ['\\', '/', ':', '*', '?', '"', '<', '>', '|']
            for i in range(blob_header.local_entry_cnt):
                print(names[i])
                file_oldname = assemblies_dir_path + '/%d.dll' % (i)
                for check in not_to_name:
                    if check in names[i]:
                        names[i] = names[i].replace(check, '')

                file_newname = assemblies_dir_path + '/' + names[i] + '.dll'
                os.rename(file_oldname, file_newname)

        print(' > Finished extracting dlls from \'%s\'.\n' % (assemblies_path))
        return True

def decompress_dlls(assemblies_dir_path, dll_path):
    file_list = os.listdir(assemblies_dir_path)
    dll_list = [file for file in file_list if file.endswith(".dll")]
    print(f' > Found {len(dll_list)} dlls.')
    xalz_magic = b'XALZ'

    for i in range(len(dll_list)):
        origin_path = assemblies_dir_path + '/' + dll_list[i]
        new_path = dll_path + '/' + dll_list[i]
        #print(' => Reading ' + origin_path)
        with open(origin_path, "rb") as dll_file:
            data = dll_file.read()
            dll_file.close()

        if data[:4] != xalz_magic:
            shutil.copy(origin_path, new_path)
        else:
            header_index = data[4:8]
            header_uncompressed_length = struct.unpack('<I', data[8:12])[0]
            payload = data[12:]

            #print("  => header index: %s" % header_index)
            #print("  => compressed payload size: %s bytes" % len(payload))
            #print("  => uncompressed length according to header: %s bytes" % header_uncompressed_length)

            decompressed = lz4.block.decompress(payload, uncompressed_size=header_uncompressed_length)

            with open(new_path, "wb") as output_file:
                output_file.write(decompressed)
                output_file.close()

    return True


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print_usage_and_exit()

    apk_directory = sys.argv[1]
    print(' > target directory is ' + apk_directory)

    assemblies_dir_path = apk_directory + '/assemblies'
    dll_path = apk_directory + '/dlls'

    step1 = extract_dlls(assemblies_dir_path)
    try:
        if not os.path.exists(dll_path):
            os.makedirs(dll_path)
    except OSError:
        print("Error: Failed to create the directory.")

    if not step1:
        print(' > There is not \'%s\' ' % (assemblies_dir_path + 'assemblies.blob'))

    print(' > Make directory \'%s\'' % (dll_path))


    step2 = decompress_dlls(assemblies_dir_path, dll_path)
    if step2 :
        print(' > Finished extracting normal dlls.')