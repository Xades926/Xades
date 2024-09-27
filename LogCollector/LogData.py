import os
import csv


class LogData:
    def __init__(self):
        self.apk_dir = ''
        self.apk_name = ''

        # Result of Csharp Analyzer
        self.dll_cnt = 0

        self.cg_node = 0
        self.cg_edge = 0
        # cpp wrappers
        self.cpp_node = 0
        self.cpp_edge = 0
        self.cpp_custom_edges = 0
        # java wrappers
        self.java_node = 0
        self.java_edge = 0
        self.java_custom_edges = 0

        self.tainted_node = 0
        self.tainted_edge = 0

        self.convert_jimple = 'x'
        # self.activity_cnt = 0  # Android activity
        #
        # self.CsharpSource_cnt = 0
        # self.CsharpSink_cnt = 0
        # self.CsharpBoth_cnt = 0
        self.CsharpLeak_cnt = 0
        #
        # self.JavaSource_cnt = 0
        # self.JavaSink_cnt = 0
        # self.JavaBoth_cnt = 0
        self.JavaLeak_cnt = 0
        #
        # self.CppSource_cnt = 0
        # self.CppSink_cnt = 0
        # self.CppBoth_cnt = 0
        self.CppLeak_cnt = 0
        self.memoryUsage = 0.0  # memory usage
        self.runningTime = 0.0  # running time
        self.csharp_error = ''


        # FlowDroid Analysis
        self.fd_cg_node1 = 0
        self.fd_cg_edge1 = 0

        self.fd_cg_node2 = 0
        self.fd_cg_edge2 = 0
        self.fd_time = 0
        self.ndk_edge_cnt = 0
        self.ndk_lib_cnt = 0
        self.java_memoryUsage = 0
        self.java_time = 0

        self.total_leaks = 0
        self.java_error = ''

    def write_csv(self, output_path):
        fieldnames = ['apkDir', 'apk']
        # 'cppCustom_cnt', 'cppWrapper_cnt'
        csharp_fieldnames = ['dlls', 'cg_node', 'cg_edge',
                             'cpp_node', 'cpp_edge', 'cpp_custom_edges',
                             'java_node', 'java_edge', 'java_custom_edges',
                             'tainted_node', 'tainted_edge', 'convert_jimple',
                             #'CsharpSource', 'CsharpSink', 'CsharpBoth', 'CsharpLeak',
                             #'activity', 'JavaSource','JavaSink','JavaBoth','JavaLeak',
                             #'CppSource', 'CppSink', 'CppBoth',
                             'CsharpLeak', 'JavaLeak','CppLeak',
                             'memoryUsage', 'runningTime', 'csharp_error']
        java_fieldnames = ['fd_cg_edge1', 'fd_cg_edge2',
                           'ndk_edge_cnt', 'ndk_lib_cnt',
                           'java_memoryUsage', 'fd_time',
                           'total_leaks', 'java_error'
                           ]
        for field in csharp_fieldnames:
            fieldnames.append(field)

        for field in java_fieldnames:
            fieldnames.append(field)

        if not os.path.exists(output_path):
            with open(output_path, 'w') as csvfile:
                csvfile.write(",".join(map(str,fieldnames)))
                csvfile.write("\n")
                csvfile.close()

        if len(self.java_error) > 0 or len(self.csharp_error):
            print('ERROR')
        else:
            print('OK')

        with open(output_path, 'a', newline='') as csvfile:
            writer = csv.DictWriter(csvfile, fieldnames=fieldnames)
            writer.writerow({
                'apkDir':self.apk_dir, 'apk':self.apk_name,
                'dlls':self.dll_cnt, 'cg_node':self.cg_node, 'cg_edge':self.cg_edge,
                'cpp_node':self.cpp_node, 'cpp_edge':self.cpp_edge, 'cpp_custom_edges':self.cpp_custom_edges,
                'java_node': self.java_node, 'java_edge': self.java_edge, 'java_custom_edges': self.java_custom_edges,
                'tainted_node':self.tainted_node, 'tainted_edge':self.tainted_edge, 'convert_jimple':self.convert_jimple,
                # 'activity':self.activity_cnt,
                # 'CsharpSource': self.CsharpSource_cnt, 'CsharpSink': self.CsharpSink_cnt, 'CsharpBoth': self.CsharpBoth_cnt,
                # 'JavaSource':self.JavaSource_cnt, 'JavaSink':self.JavaSink_cnt, 'JavaBoth':self.JavaBoth_cnt,
                # 'CppSource': self.CppSource_cnt, 'CppSink': self.CppSink_cnt, 'CppBoth': self.CppBoth_cnt,
                'CsharpLeak': self.CsharpLeak_cnt, 'JavaLeak':self.JavaLeak_cnt, 'CppLeak': self.CppLeak_cnt,
                'memoryUsage':self.memoryUsage, 'runningTime':self.runningTime, 'csharp_error':self.csharp_error,
                'fd_cg_edge1':self.fd_cg_edge1,'fd_cg_edge2':self.fd_cg_edge2,
                'ndk_edge_cnt':self.ndk_edge_cnt, 'ndk_lib_cnt':self.ndk_lib_cnt,
                'java_memoryUsage':self.java_memoryUsage, 'fd_time':self.java_time,
                'total_leaks':self.total_leaks, 'java_error':self.java_error
            })
            csvfile.close()
