import matplotlib.pyplot as plt
import csv



def chart(filePath, testIDList):
    with open(filePath) as file:
        
        reader = csv.reader(file)
        firstRow = True
        i = 0

        for row in reader:
            
            if firstRow:
                firstRow = False
                continue

            if int(row[0]) not in testIDList:
                continue

            if i == 5:
                break

            i += 1

            x = [i + 1 for i in range(len(row[6].split("|")))]
            y = [int(i) for i in row[6].split("|")]
            plt.plot(x, y, label = f"NR testu: {int(row[0]) - 25}")
        
        plt.grid()
        plt.legend()
        plt.xlabel("Numer generacji")
        plt.ylabel("Wartość funkcji celu")
        plt.show()



def main():

    chart("data/outputs/austria/austria_3.csv", [46, 47, 49, 52, 55])
    chart("data/outputs/belgium/belgium_3.csv", [42, 46, 49, 52, 55])
    chart("data/outputs/czech-republic/czech-republic_4.csv", [47, 49, 50, 52, 55])
    chart("data/outputs/germany/germany_4.csv", [46, 49, 51, 52, 54])
    chart("data/outputs/hungary/hungary_1.csv", [41, 46, 47, 50, 52])
    chart("data/outputs/netherlands/netherlands_2.csv", [46, 47, 49, 52, 55])
    chart("data/outputs/poland/poland_1.csv", [42, 51, 52, 54, 55])
    chart("data/outputs/slovakia/slovakia_1.csv", [45, 46, 50, 52, 55])



main()