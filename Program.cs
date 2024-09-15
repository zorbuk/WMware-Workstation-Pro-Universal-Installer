using System.Diagnostics;
using System.Formats.Tar;

string WMwareRepository = "https://softwareupdate.vmware.com/cds/vmw-desktop/ws/";

HttpClient httpClient = new HttpClient();
Stream fileStream;
int desiredVersionIndex = -1, versionIndex = 0, desiredOs = -1, osIndex = 0;
string tempFolderLocation = "./vmware-temp", tempFileName = "vmware.tar";

List<String> parsePageLists(string htmlContent)
{
    string[] helper = htmlContent.Split("<li><a href=\"", StringSplitOptions.None);
    List<String> listContent = new List<string>();

    int listIndex = 0;
    foreach (string helperItem in helper)
    {
        if (listIndex > 1)
        {
            listContent.Add(helperItem.Split("\">", StringSplitOptions.None)[0].Replace("/",""));
        }
        listIndex++;
    }

    return listContent;
}

async Task<List<String>> getVMWareVersions()
{

    var response = await httpClient.GetAsync(WMwareRepository);
    if (response.IsSuccessStatusCode)
    {
        string htmlContent = await response.Content.ReadAsStringAsync();
        List<String> versions = parsePageLists(htmlContent);
        return versions;
    }

    return null;
}

List<String> versions = await getVMWareVersions();

if(versions ==null)
{
    Console.WriteLine("¡Error!\nWe did not found any version of VMware... \n¿Maybe version repository is down?\nRepository Source: https://softwareupdate.vmware.com/cds/vmw-desktop/ws/");
    return;
}

foreach (string version in versions)
{
    Console.WriteLine($"{versionIndex}\t > VMware Workstation Pro {version}");
    versionIndex++;
}

Console.Write("\nSelect your desired VMware Workstation Pro version: ");

try
{
    desiredVersionIndex = int.Parse(Console.ReadLine());
}catch(Exception e)
{
    Console.WriteLine("¡Error!\nInvalid input, only numbers allowed!");
    return;
}

if (versionIndex-1 < desiredVersionIndex || desiredVersionIndex < 0) { 
    Console.WriteLine("¡Error!\nWrong version selected.");
    return;
}

else
{
    Console.WriteLine($"\nYou're going to install: VMware Workstation Pro {versions[desiredVersionIndex]}\n");

    async Task<String> getVMsignature()
    {
        var response = await httpClient.GetAsync($"{WMwareRepository}{versions[desiredVersionIndex]}/");
        if (response.IsSuccessStatusCode)
        {
            string htmlContent = await response.Content.ReadAsStringAsync();
            List<String> signatures = parsePageLists(htmlContent);
            return signatures[0];
        }

        return null;
    }

    String vmSignature = await getVMsignature();

    if(vmSignature == null)
    {
        Console.WriteLine($"¡Error!\nWe did not found signature: {WMwareRepository}{versions[desiredVersionIndex]}/");
        return;
    }

    async Task<List<String>> getOs(string signature)
    {
        var response = await httpClient.GetAsync($"{WMwareRepository}{versions[desiredVersionIndex]}/{signature}/");
        if (response.IsSuccessStatusCode)
        {
            string htmlContent = await response.Content.ReadAsStringAsync();
            List<String> oss = parsePageLists(htmlContent);
            return oss;
        }

        return null;
    }

    List<String> oss = await getOs(vmSignature);

    if (oss == null)
    {
        Console.WriteLine($"¡Error!\nWe did not found any os version available of VMware {versions[desiredVersionIndex]} ...");
        return;
    }

    foreach (string os in oss)
    {
        Console.WriteLine($"{osIndex}\t > {os}");
        osIndex++;
    }

    Console.Write("\nSelect your desired OS: ");
    try
    {
        desiredOs = int.Parse(Console.ReadLine());
    }catch (Exception e)
    {
        Console.WriteLine("¡Error!\nInvalid input, only numbers allowed!");
        return;
    }

    if (osIndex - 1 < desiredOs || desiredOs < 0)
    {
        Console.WriteLine("¡Error!\nWrong OS selected.");
        return;
    }
    else
    {
        Console.WriteLine($"\nSelected OS: {oss[desiredOs]}");

        void clearTempRepository()
        {
            if (Directory.Exists(tempFolderLocation))
            {
                Directory.Delete(tempFolderLocation, true);
            }
        }

        clearTempRepository();

        async Task<String> getZipTarUrl(string signature)
        {
            var response = await httpClient.GetAsync($"{WMwareRepository}{versions[desiredVersionIndex]}/{signature}/{oss[desiredOs]}/core/");
            if (response.IsSuccessStatusCode)
            {
                string htmlContent = await response.Content.ReadAsStringAsync();
                List<String> downloadSources = parsePageLists(htmlContent);
                return $"{WMwareRepository}{versions[desiredVersionIndex]}/{signature}/{oss[desiredOs]}/core/{downloadSources[0]}";
            }

            return null;
        }
        async Task<Stream> download(string url)
        {
            Console.WriteLine($"\nDownloading, please wait ...");
            HttpClient httpClient = new HttpClient();
            try
            {
                Stream fileStream = await httpClient.GetStreamAsync(url);
                return fileStream;
            }
            catch (Exception ex)
            {
                return Stream.Null;
            }
        }
        async Task save()
        {
            if (!Directory.Exists(tempFolderLocation))
                Directory.CreateDirectory(tempFolderLocation);

            string path = Path.Combine(tempFolderLocation, tempFileName);

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using (FileStream outputFileStream = new FileStream(path, FileMode.CreateNew))
            {
                await fileStream.CopyToAsync(outputFileStream);
            }
        }
        async Task extract()
        {
            string path = Path.Combine(tempFolderLocation, tempFileName);
            await TarFile.ExtractToDirectoryAsync(path, tempFolderLocation, true);
        }
        void execute(string url)
        {
            string file = url.Split("/core/", StringSplitOptions.None)[1].Replace(".tar","");
            string path = Path.Combine(tempFolderLocation, file);
            Process proc = new Process();
            proc.StartInfo.FileName = path;
            proc.StartInfo.UseShellExecute = true;
            proc.StartInfo.Verb = "runas";
            try
            {
                proc.Start();
            }
            catch (Exception ex) {
                Console.WriteLine($"\nInstallation was cancelled because administrator rights are required.");
            }
        }

        switch (oss[desiredOs])
        {
            case "linux":
                Console.WriteLine("Linux distro is not available atm.");
                break;
            case "windows":
                string zipTar = await getZipTarUrl(vmSignature);
                fileStream = await download(zipTar);
                await save();
                await extract();
                execute(zipTar);
                Console.WriteLine($"\nYour VMware Workstation Pro {versions[desiredVersionIndex]} for {oss[desiredOs]} is ready to be installed. ¡Enjoy!");
                break;
            default:
                Console.WriteLine("Unsupported OS");
                return;
        }
    }
}