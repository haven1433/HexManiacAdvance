# python -m bump
# python -m bump minor
# python -m bump major
import sys

def main(arg):
    lines = []
    new_version = ''
    with open('src/SharedAssemblyInfo.cs') as file:
        for line in file:
            lines.append(line)

    with open('src/SharedAssemblyInfo.cs', 'w') as file:
        for line in lines:
            if not 'AssemblyVersion' in line and not 'AssemblyFileVersion' in line:
                file.write(line)
                continue
            parts = line.split('"')
            version = parts[1].split('.')
            while len(version) < 4: version.append('0')
            i = 3
            if arg == 'major': i = 1
            if arg == 'minor': i = 2
            version[i] = str(int(version[i])+1)
            if i < 3: version[3] = '0'
            if i < 2: version[2] = '0'
            if version[3] == '0': del version[3]
            parts[1] = '.'.join(version)
            file.write('"'.join(parts))
            new_version = '.'.join(version)

    return new_version

if __name__ == '__main__':
    arg = sys.argv[1] if len(sys.argv) > 1 else ''
    print(main(arg))
