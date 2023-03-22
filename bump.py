# python -m bump build
# python -m bump update
# python -m bump minor
# python -m bump major
import sys

def main(arg):
    lines = []
    new_version = ''
    file_path = 'src/SharedAssemblyInfo.cs'
    with open(file_path) as file:
        for line in file:
            lines.append(line)

    with open(file_path, 'w') as file:
        for line in lines:
            if not 'AssemblyVersion' in line and not 'AssemblyFileVersion' in line:
                file.write(line)
                continue
            parts = line.split('"')
            version = parts[1].split('.')
            while len(version) < 4: version.append('0')
            i = 3
            if arg == 'major': i = 0
            if arg == 'minor': i = 1
            if arg == 'update': i = 2
            version[i] = str(int(version[i])+1)
            if i < 3: version[3] = '0'
            if i < 2: version[2] = '0'
            if version[3] == '0': del version[3]
            new_version = '.'.join(version)
            parts[1] = new_version
            file.write('"'.join(parts))

    return new_version

if __name__ == '__main__':
    arg = sys.argv[1] if len(sys.argv) > 1 else ''
    print(main(arg))
