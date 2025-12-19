with open('build_errors.txt', 'rb') as f:
    content = f.read()
    try:
        # Try UTF-16 first as dotnet sometimes outputs it
        text = content.decode('utf-16')
    except:
        try:
            text = content.decode('utf-8')
        except:
            text = content.decode('cp1252')
print(text)
