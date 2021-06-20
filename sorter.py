import json, sys
from tkinter import Tk, filedialog
from tkinter.filedialog import askopenfilename
from difflib import SequenceMatcher
from IPython import get_ipython
get_ipython().magic('reset -sf') 

# From https://stackoverflow.com/questions/19476232/save-file-dialog-in-tkinter, I'm lazy
def file_save(text):
    f = filedialog.asksaveasfile(mode='w', defaultextension=".json", filetypes=[('JSON Files','*.json')], title="Save the sorted JSON", initialfile='sorted')
    if f is None: # asksaveasfile return `None` if dialog closed with "cancel".
        return
    text2save = text.replace("\'","\"") # starts from `1.0`, not `0.0`
    f.seek(0)
    f.truncate()
    f.write(text2save)
    f.close() # `()` was missing.   

root = Tk()
root.call('wm', 'attributes', '.', '-topmost', True)
root.withdraw()
filename = askopenfilename(title = "Select existing JSON for sorting", filetypes=[('JSON Files','*.json')]);
if not filename:
    sys.exit("No file selected")
    
desired = input("Paste the desired load order, with each mod title on a seperate line:\n")
typocon = float(input("Define typo confidence (0.00-1.00, default = 0.50): ") or 0.50)
if (typocon < 0 or typocon > 1):
    sys.exit("Invalid input for typo confidence.")
elif (typocon < 0.5 or typocon > 0.7):
    print('\nToo low or high typo confidence can result in mods not being found and therefore properly sorted\n')
#sortfilt = input("Mod ID (1)\nSteam ID (2)\nName (3)\nChoose sorting filter: ")
#if (sortfilt <= 0 or sortfilt > 3):
#    sys.exit("Sort filter selection out of bounds [1-3]")

splitDesires = desired.split("\n")
done = []
with open(filename) as f:
    og = json.loads(f.read())
    for i, tosort in enumerate(splitDesires):
        highestConfidence = float(0)
        highConMod = None
        for mod in og["Mods"]:
            #print('=== Looking for mod ===', end="")
            thisRatio = SequenceMatcher(None, a=splitDesires[i],b=mod["DisplayName"]).ratio()
            #print('Comparing mod ' + str(mod["DisplayName"]) + ' to ' + str(splitDesires[i]) + ': ' + str(thisRatio) + '\n')
            if (thisRatio > highestConfidence):
                highestConfidence = thisRatio
                if (thisRatio > typocon):
                    highConMod = mod
        if highConMod is not None:
            #print('\n     Mod ' + str(highConMod["steamId"]) + ' (\"' + str(highConMod["DisplayName"]) + '\") was selected for \"' + str(splitDesires[i]) + '\" with a confidence of ' + str(highestConfidence) + '\n' + str(highConMod))
            highConMod["DisplayName"] = highConMod["DisplayName"].replace('\'','')
            done.append(highConMod)
        else:
            print('\nNo sequence within confidence bounds matches mod \"' + str(splitDesires[i]) + '\", for which the highest confidence was ' + str(round(highestConfidence,3)) + '. Perhaps the mod\'s name has unrecognized special characters. Lowering the confidence threshold should help with this, but use caution.\n')
   
    #Switch playlist positions with previous configuration
    done = list({ each['steamId'] : each for each in done }.values())
    for i, mod in enumerate(done):
        ppp = og["Mods"][i]["PlaysetPosition"]
        print("Mod #" + str(i) + "\'s playset position has changed from " + mod["PlaysetPosition"] + ' to ' + str(ppp))
        mod["PlaysetPosition"] = ppp
        
        
    #print('Finished sorting. Here is the new JSON:\n\n' + str(done).replace("},","}\n"))
    og["Mods"] = done
    #print('Orgs mods is now:\n' + str(og["Mods"]).replace('},','}\n') + '\n\n')
    #print('Resulting in \n\n' + str(og))
    file_save(json.dumps(og, separators=(',', ':')))
#print(done)

for mod in og["Mods"]:
    print(mod["DisplayName"])

             
