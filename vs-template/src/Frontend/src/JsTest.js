import axios from 'axios';

export class MathUtils {

    static square(x) {
        return x * x;
    }

    static distance(x1, y1, x2, y2) {
        return Math.sqrt(this.square(x2 - x1) + this.square(y2 - y1));
    }

    static getCurrentTimestamp() {
        return new Date().toISOString();
    }

    /**
    * @async
    */
    static async fetchDataFromAPI() {
        let response = await fetch("https://api.example.com/data");
        return await response.text();
    }

    /**
     * @async
     * @dependency import axios from 'axios';
     */
    static async fetchDataUsingAxios(url) {
        const response = await axios.get(url);
        return response.data;
    }
}
