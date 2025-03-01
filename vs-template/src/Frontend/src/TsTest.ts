export class MathUtils {
    static square(x: number): number {
        return x * x;
    }

    static distance(x1: number, y1: number, x2: number, y2: number): number {
        return Math.sqrt(this.square(x2 - x1) + this.square(y2 - y1));
    }

    static getCurrentTimestamp(): string {
        return new Date().toISOString();
    }

    static async fetchDataFromAPI(): Promise<string> {
        const response = await fetch("https://api.example.com/data");
        return await response.text();
    }
}
