/*
   Licensed to the Apache Software Foundation (ASF) under one or more
   contributor license agreements.  See the NOTICE file distributed with
   this work for additional information regarding copyright ownership.
   The ASF licenses this file to You under the Apache License, Version 2.0
   (the "License"); you may not use this file except in compliance with
   the License.  You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
$(document).ready(function() {

    $(".click-title").mouseenter( function(    e){
        e.preventDefault();
        this.style.cursor="pointer";
    });
    $(".click-title").mousedown( function(event){
        event.preventDefault();
    });

    // Ugly code while this script is shared among several pages
    try{
        refreshHitsPerSecond(true);
    } catch(e){}
    try{
        refreshResponseTimeOverTime(true);
    } catch(e){}
    try{
        refreshResponseTimePercentiles();
    } catch(e){}
});


var responseTimePercentilesInfos = {
        data: {"result": {"minY": 1681.0, "minX": 0.0, "maxY": 9040.0, "series": [{"data": [[0.0, 1681.0], [0.1, 1681.0], [0.2, 1681.0], [0.3, 1681.0], [0.4, 2112.0], [0.5, 2112.0], [0.6, 2112.0], [0.7, 2112.0], [0.8, 2132.0], [0.9, 2132.0], [1.0, 2132.0], [1.1, 2132.0], [1.2, 2132.0], [1.3, 2170.0], [1.4, 2170.0], [1.5, 2170.0], [1.6, 2186.0], [1.7, 2186.0], [1.8, 2186.0], [1.9, 2186.0], [2.0, 2192.0], [2.1, 2192.0], [2.2, 2192.0], [2.3, 2192.0], [2.4, 2287.0], [2.5, 2287.0], [2.6, 2287.0], [2.7, 2287.0], [2.8, 2301.0], [2.9, 2301.0], [3.0, 2301.0], [3.1, 2301.0], [3.2, 2311.0], [3.3, 2311.0], [3.4, 2311.0], [3.5, 2311.0], [3.6, 2335.0], [3.7, 2335.0], [3.8, 2335.0], [3.9, 2335.0], [4.0, 2340.0], [4.1, 2340.0], [4.2, 2340.0], [4.3, 2340.0], [4.4, 2348.0], [4.5, 2348.0], [4.6, 2348.0], [4.7, 2348.0], [4.8, 2400.0], [4.9, 2400.0], [5.0, 2400.0], [5.1, 2400.0], [5.2, 2424.0], [5.3, 2424.0], [5.4, 2424.0], [5.5, 2424.0], [5.6, 2424.0], [5.7, 2430.0], [5.8, 2430.0], [5.9, 2430.0], [6.0, 2430.0], [6.1, 2492.0], [6.2, 2492.0], [6.3, 2492.0], [6.4, 2492.0], [6.5, 2553.0], [6.6, 2553.0], [6.7, 2553.0], [6.8, 2553.0], [6.9, 2559.0], [7.0, 2559.0], [7.1, 2559.0], [7.2, 2559.0], [7.3, 2563.0], [7.4, 2563.0], [7.5, 2563.0], [7.6, 2563.0], [7.7, 2612.0], [7.8, 2612.0], [7.9, 2612.0], [8.0, 2612.0], [8.1, 2636.0], [8.2, 2636.0], [8.3, 2636.0], [8.4, 2636.0], [8.5, 2642.0], [8.6, 2642.0], [8.7, 2642.0], [8.8, 2642.0], [8.9, 2674.0], [9.0, 2674.0], [9.1, 2674.0], [9.2, 2674.0], [9.3, 2693.0], [9.4, 2693.0], [9.5, 2693.0], [9.6, 2693.0], [9.7, 2694.0], [9.8, 2694.0], [9.9, 2694.0], [10.0, 2694.0], [10.1, 2698.0], [10.2, 2698.0], [10.3, 2698.0], [10.4, 2698.0], [10.5, 2708.0], [10.6, 2708.0], [10.7, 2708.0], [10.8, 2708.0], [10.9, 2710.0], [11.0, 2710.0], [11.1, 2710.0], [11.2, 2710.0], [11.3, 2728.0], [11.4, 2728.0], [11.5, 2728.0], [11.6, 2728.0], [11.7, 2791.0], [11.8, 2791.0], [11.9, 2791.0], [12.0, 2791.0], [12.1, 2808.0], [12.2, 2808.0], [12.3, 2808.0], [12.4, 2808.0], [12.5, 2816.0], [12.6, 2816.0], [12.7, 2816.0], [12.8, 2816.0], [12.9, 2834.0], [13.0, 2834.0], [13.1, 2834.0], [13.2, 2834.0], [13.3, 2836.0], [13.4, 2836.0], [13.5, 2836.0], [13.6, 2836.0], [13.7, 2846.0], [13.8, 2846.0], [13.9, 2846.0], [14.0, 2846.0], [14.1, 2848.0], [14.2, 2848.0], [14.3, 2848.0], [14.4, 2848.0], [14.5, 2862.0], [14.6, 2862.0], [14.7, 2862.0], [14.8, 2862.0], [14.9, 2873.0], [15.0, 2873.0], [15.1, 2873.0], [15.2, 2873.0], [15.3, 2884.0], [15.4, 2884.0], [15.5, 2884.0], [15.6, 2884.0], [15.7, 2887.0], [15.8, 2887.0], [15.9, 2887.0], [16.0, 2887.0], [16.1, 2895.0], [16.2, 2895.0], [16.3, 2895.0], [16.4, 2895.0], [16.5, 2938.0], [16.6, 2938.0], [16.7, 2938.0], [16.8, 2938.0], [16.9, 2944.0], [17.0, 2944.0], [17.1, 2944.0], [17.2, 2944.0], [17.3, 2948.0], [17.4, 2948.0], [17.5, 2948.0], [17.6, 2960.0], [17.7, 2960.0], [17.8, 2960.0], [17.9, 2960.0], [18.0, 2967.0], [18.1, 2967.0], [18.2, 2967.0], [18.3, 2967.0], [18.4, 2982.0], [18.5, 2982.0], [18.6, 2982.0], [18.7, 2982.0], [18.8, 2985.0], [18.9, 2985.0], [19.0, 2985.0], [19.1, 2985.0], [19.2, 2989.0], [19.3, 2989.0], [19.4, 2989.0], [19.5, 2989.0], [19.6, 2992.0], [19.7, 2992.0], [19.8, 2992.0], [19.9, 2992.0], [20.0, 2992.0], [20.1, 2992.0], [20.2, 2992.0], [20.3, 2992.0], [20.4, 2996.0], [20.5, 2996.0], [20.6, 2996.0], [20.7, 2996.0], [20.8, 3000.0], [20.9, 3000.0], [21.0, 3000.0], [21.1, 3000.0], [21.2, 3011.0], [21.3, 3011.0], [21.4, 3011.0], [21.5, 3011.0], [21.6, 3020.0], [21.7, 3020.0], [21.8, 3020.0], [21.9, 3020.0], [22.0, 3022.0], [22.1, 3022.0], [22.2, 3022.0], [22.3, 3022.0], [22.4, 3023.0], [22.5, 3023.0], [22.6, 3023.0], [22.7, 3023.0], [22.8, 3030.0], [22.9, 3030.0], [23.0, 3030.0], [23.1, 3030.0], [23.2, 3030.0], [23.3, 3030.0], [23.4, 3030.0], [23.5, 3030.0], [23.6, 3030.0], [23.7, 3030.0], [23.8, 3030.0], [23.9, 3030.0], [24.0, 3041.0], [24.1, 3041.0], [24.2, 3041.0], [24.3, 3041.0], [24.4, 3065.0], [24.5, 3065.0], [24.6, 3065.0], [24.7, 3065.0], [24.8, 3069.0], [24.9, 3069.0], [25.0, 3069.0], [25.1, 3069.0], [25.2, 3071.0], [25.3, 3071.0], [25.4, 3071.0], [25.5, 3071.0], [25.6, 3074.0], [25.7, 3074.0], [25.8, 3074.0], [25.9, 3074.0], [26.0, 3077.0], [26.1, 3077.0], [26.2, 3077.0], [26.3, 3077.0], [26.4, 3079.0], [26.5, 3079.0], [26.6, 3079.0], [26.7, 3079.0], [26.8, 3083.0], [26.9, 3083.0], [27.0, 3083.0], [27.1, 3083.0], [27.2, 3084.0], [27.3, 3084.0], [27.4, 3084.0], [27.5, 3084.0], [27.6, 3086.0], [27.7, 3086.0], [27.8, 3086.0], [27.9, 3086.0], [28.0, 3089.0], [28.1, 3089.0], [28.2, 3089.0], [28.3, 3089.0], [28.4, 3111.0], [28.5, 3111.0], [28.6, 3111.0], [28.7, 3111.0], [28.8, 3128.0], [28.9, 3128.0], [29.0, 3128.0], [29.1, 3128.0], [29.2, 3132.0], [29.3, 3132.0], [29.4, 3132.0], [29.5, 3132.0], [29.6, 3141.0], [29.7, 3141.0], [29.8, 3141.0], [29.9, 3141.0], [30.0, 3143.0], [30.1, 3143.0], [30.2, 3143.0], [30.3, 3143.0], [30.4, 3152.0], [30.5, 3152.0], [30.6, 3152.0], [30.7, 3152.0], [30.8, 3155.0], [30.9, 3155.0], [31.0, 3155.0], [31.1, 3155.0], [31.2, 3179.0], [31.3, 3179.0], [31.4, 3179.0], [31.5, 3179.0], [31.6, 3185.0], [31.7, 3185.0], [31.8, 3185.0], [31.9, 3185.0], [32.0, 3186.0], [32.1, 3186.0], [32.2, 3186.0], [32.3, 3186.0], [32.4, 3198.0], [32.5, 3198.0], [32.6, 3198.0], [32.7, 3198.0], [32.8, 3206.0], [32.9, 3206.0], [33.0, 3206.0], [33.1, 3206.0], [33.2, 3210.0], [33.3, 3210.0], [33.4, 3210.0], [33.5, 3210.0], [33.6, 3219.0], [33.7, 3219.0], [33.8, 3219.0], [33.9, 3219.0], [34.0, 3234.0], [34.1, 3234.0], [34.2, 3234.0], [34.3, 3234.0], [34.4, 3244.0], [34.5, 3244.0], [34.6, 3244.0], [34.7, 3244.0], [34.8, 3248.0], [34.9, 3248.0], [35.0, 3248.0], [35.1, 3248.0], [35.2, 3256.0], [35.3, 3256.0], [35.4, 3256.0], [35.5, 3256.0], [35.6, 3261.0], [35.7, 3261.0], [35.8, 3261.0], [35.9, 3261.0], [36.0, 3261.0], [36.1, 3261.0], [36.2, 3261.0], [36.3, 3261.0], [36.4, 3263.0], [36.5, 3263.0], [36.6, 3263.0], [36.7, 3263.0], [36.8, 3269.0], [36.9, 3269.0], [37.0, 3269.0], [37.1, 3269.0], [37.2, 3300.0], [37.3, 3300.0], [37.4, 3300.0], [37.5, 3300.0], [37.6, 3305.0], [37.7, 3305.0], [37.8, 3305.0], [37.9, 3305.0], [38.0, 3307.0], [38.1, 3307.0], [38.2, 3307.0], [38.3, 3307.0], [38.4, 3310.0], [38.5, 3310.0], [38.6, 3310.0], [38.7, 3310.0], [38.8, 3312.0], [38.9, 3312.0], [39.0, 3312.0], [39.1, 3312.0], [39.2, 3331.0], [39.3, 3331.0], [39.4, 3331.0], [39.5, 3331.0], [39.6, 3331.0], [39.7, 3331.0], [39.8, 3331.0], [39.9, 3331.0], [40.0, 3333.0], [40.1, 3333.0], [40.2, 3333.0], [40.3, 3333.0], [40.4, 3334.0], [40.5, 3334.0], [40.6, 3334.0], [40.7, 3334.0], [40.8, 3339.0], [40.9, 3339.0], [41.0, 3339.0], [41.1, 3339.0], [41.2, 3341.0], [41.3, 3341.0], [41.4, 3341.0], [41.5, 3341.0], [41.6, 3346.0], [41.7, 3346.0], [41.8, 3346.0], [41.9, 3346.0], [42.0, 3356.0], [42.1, 3356.0], [42.2, 3356.0], [42.3, 3356.0], [42.4, 3359.0], [42.5, 3359.0], [42.6, 3359.0], [42.7, 3359.0], [42.8, 3367.0], [42.9, 3367.0], [43.0, 3367.0], [43.1, 3367.0], [43.2, 3371.0], [43.3, 3371.0], [43.4, 3371.0], [43.5, 3371.0], [43.6, 3371.0], [43.7, 3371.0], [43.8, 3371.0], [43.9, 3371.0], [44.0, 3386.0], [44.1, 3386.0], [44.2, 3386.0], [44.3, 3386.0], [44.4, 3388.0], [44.5, 3388.0], [44.6, 3388.0], [44.7, 3388.0], [44.8, 3400.0], [44.9, 3400.0], [45.0, 3400.0], [45.1, 3400.0], [45.2, 3401.0], [45.3, 3401.0], [45.4, 3401.0], [45.5, 3401.0], [45.6, 3401.0], [45.7, 3401.0], [45.8, 3401.0], [45.9, 3401.0], [46.0, 3417.0], [46.1, 3417.0], [46.2, 3417.0], [46.3, 3417.0], [46.4, 3435.0], [46.5, 3435.0], [46.6, 3435.0], [46.7, 3435.0], [46.8, 3442.0], [46.9, 3442.0], [47.0, 3442.0], [47.1, 3442.0], [47.2, 3445.0], [47.3, 3445.0], [47.4, 3445.0], [47.5, 3445.0], [47.6, 3449.0], [47.7, 3449.0], [47.8, 3449.0], [47.9, 3449.0], [48.0, 3465.0], [48.1, 3465.0], [48.2, 3465.0], [48.3, 3465.0], [48.4, 3480.0], [48.5, 3480.0], [48.6, 3480.0], [48.7, 3480.0], [48.8, 3482.0], [48.9, 3482.0], [49.0, 3482.0], [49.1, 3482.0], [49.2, 3487.0], [49.3, 3487.0], [49.4, 3487.0], [49.5, 3487.0], [49.6, 3495.0], [49.7, 3495.0], [49.8, 3495.0], [49.9, 3495.0], [50.0, 3511.0], [50.1, 3511.0], [50.2, 3511.0], [50.3, 3511.0], [50.4, 3524.0], [50.5, 3524.0], [50.6, 3524.0], [50.7, 3524.0], [50.8, 3524.0], [50.9, 3524.0], [51.0, 3524.0], [51.1, 3524.0], [51.2, 3528.0], [51.3, 3528.0], [51.4, 3528.0], [51.5, 3528.0], [51.6, 3541.0], [51.7, 3541.0], [51.8, 3541.0], [51.9, 3541.0], [52.0, 3548.0], [52.1, 3548.0], [52.2, 3548.0], [52.3, 3548.0], [52.4, 3562.0], [52.5, 3562.0], [52.6, 3562.0], [52.7, 3562.0], [52.8, 3576.0], [52.9, 3576.0], [53.0, 3576.0], [53.1, 3576.0], [53.2, 3577.0], [53.3, 3577.0], [53.4, 3577.0], [53.5, 3577.0], [53.6, 3612.0], [53.7, 3612.0], [53.8, 3612.0], [53.9, 3612.0], [54.0, 3636.0], [54.1, 3636.0], [54.2, 3636.0], [54.3, 3636.0], [54.4, 3637.0], [54.5, 3637.0], [54.6, 3637.0], [54.7, 3637.0], [54.8, 3645.0], [54.9, 3645.0], [55.0, 3645.0], [55.1, 3645.0], [55.2, 3649.0], [55.3, 3649.0], [55.4, 3649.0], [55.5, 3649.0], [55.6, 3651.0], [55.7, 3651.0], [55.8, 3651.0], [55.9, 3651.0], [56.0, 3660.0], [56.1, 3660.0], [56.2, 3660.0], [56.3, 3660.0], [56.4, 3675.0], [56.5, 3675.0], [56.6, 3675.0], [56.7, 3675.0], [56.8, 3698.0], [56.9, 3698.0], [57.0, 3698.0], [57.1, 3698.0], [57.2, 3699.0], [57.3, 3699.0], [57.4, 3699.0], [57.5, 3699.0], [57.6, 3721.0], [57.7, 3721.0], [57.8, 3721.0], [57.9, 3721.0], [58.0, 3723.0], [58.1, 3723.0], [58.2, 3723.0], [58.3, 3723.0], [58.4, 3749.0], [58.5, 3749.0], [58.6, 3749.0], [58.7, 3749.0], [58.8, 3758.0], [58.9, 3758.0], [59.0, 3758.0], [59.1, 3758.0], [59.2, 3797.0], [59.3, 3797.0], [59.4, 3797.0], [59.5, 3797.0], [59.6, 3829.0], [59.7, 3829.0], [59.8, 3829.0], [59.9, 3829.0], [60.0, 3834.0], [60.1, 3834.0], [60.2, 3834.0], [60.3, 3834.0], [60.4, 3842.0], [60.5, 3842.0], [60.6, 3842.0], [60.7, 3842.0], [60.8, 3842.0], [60.9, 3842.0], [61.0, 3842.0], [61.1, 3842.0], [61.2, 3844.0], [61.3, 3844.0], [61.4, 3844.0], [61.5, 3844.0], [61.6, 3865.0], [61.7, 3865.0], [61.8, 3865.0], [61.9, 3865.0], [62.0, 3876.0], [62.1, 3876.0], [62.2, 3876.0], [62.3, 3876.0], [62.4, 3877.0], [62.5, 3877.0], [62.6, 3877.0], [62.7, 3877.0], [62.8, 3889.0], [62.9, 3889.0], [63.0, 3889.0], [63.1, 3889.0], [63.2, 3925.0], [63.3, 3925.0], [63.4, 3925.0], [63.5, 3925.0], [63.6, 3934.0], [63.7, 3934.0], [63.8, 3934.0], [63.9, 3934.0], [64.0, 3946.0], [64.1, 3946.0], [64.2, 3946.0], [64.3, 3946.0], [64.4, 3984.0], [64.5, 3984.0], [64.6, 3984.0], [64.7, 3984.0], [64.8, 4064.0], [64.9, 4064.0], [65.0, 4064.0], [65.1, 4064.0], [65.2, 4066.0], [65.3, 4066.0], [65.4, 4066.0], [65.5, 4066.0], [65.6, 4066.0], [65.7, 4066.0], [65.8, 4066.0], [65.9, 4066.0], [66.0, 4070.0], [66.1, 4070.0], [66.2, 4070.0], [66.3, 4070.0], [66.4, 4073.0], [66.5, 4073.0], [66.6, 4073.0], [66.7, 4073.0], [66.8, 4082.0], [66.9, 4082.0], [67.0, 4082.0], [67.1, 4082.0], [67.2, 4088.0], [67.3, 4088.0], [67.4, 4088.0], [67.5, 4088.0], [67.6, 4109.0], [67.7, 4109.0], [67.8, 4109.0], [67.9, 4109.0], [68.0, 4145.0], [68.1, 4145.0], [68.2, 4145.0], [68.3, 4145.0], [68.4, 4147.0], [68.5, 4147.0], [68.6, 4147.0], [68.7, 4147.0], [68.8, 4159.0], [68.9, 4159.0], [69.0, 4159.0], [69.1, 4159.0], [69.2, 4166.0], [69.3, 4166.0], [69.4, 4166.0], [69.5, 4166.0], [69.6, 4166.0], [69.7, 4166.0], [69.8, 4166.0], [69.9, 4166.0], [70.0, 4176.0], [70.1, 4176.0], [70.2, 4176.0], [70.3, 4176.0], [70.4, 4222.0], [70.5, 4222.0], [70.6, 4222.0], [70.7, 4222.0], [70.8, 4233.0], [70.9, 4233.0], [71.0, 4233.0], [71.1, 4233.0], [71.2, 4267.0], [71.3, 4267.0], [71.4, 4267.0], [71.5, 4267.0], [71.6, 4282.0], [71.7, 4282.0], [71.8, 4282.0], [71.9, 4282.0], [72.0, 4297.0], [72.1, 4297.0], [72.2, 4297.0], [72.3, 4297.0], [72.4, 4303.0], [72.5, 4303.0], [72.6, 4303.0], [72.7, 4303.0], [72.8, 4317.0], [72.9, 4317.0], [73.0, 4317.0], [73.1, 4317.0], [73.2, 4328.0], [73.3, 4328.0], [73.4, 4328.0], [73.5, 4328.0], [73.6, 4361.0], [73.7, 4361.0], [73.8, 4361.0], [73.9, 4361.0], [74.0, 4371.0], [74.1, 4371.0], [74.2, 4371.0], [74.3, 4371.0], [74.4, 4392.0], [74.5, 4392.0], [74.6, 4392.0], [74.7, 4392.0], [74.8, 4401.0], [74.9, 4401.0], [75.0, 4401.0], [75.1, 4401.0], [75.2, 4419.0], [75.3, 4419.0], [75.4, 4419.0], [75.5, 4419.0], [75.6, 4443.0], [75.7, 4443.0], [75.8, 4443.0], [75.9, 4443.0], [76.0, 4469.0], [76.1, 4469.0], [76.2, 4469.0], [76.3, 4469.0], [76.4, 4492.0], [76.5, 4492.0], [76.6, 4492.0], [76.7, 4492.0], [76.8, 4534.0], [76.9, 4534.0], [77.0, 4534.0], [77.1, 4534.0], [77.2, 4538.0], [77.3, 4538.0], [77.4, 4538.0], [77.5, 4538.0], [77.6, 4538.0], [77.7, 4578.0], [77.8, 4578.0], [77.9, 4578.0], [78.0, 4578.0], [78.1, 4579.0], [78.2, 4579.0], [78.3, 4579.0], [78.4, 4579.0], [78.5, 4626.0], [78.6, 4626.0], [78.7, 4626.0], [78.8, 4626.0], [78.9, 4652.0], [79.0, 4652.0], [79.1, 4652.0], [79.2, 4652.0], [79.3, 4656.0], [79.4, 4656.0], [79.5, 4656.0], [79.6, 4656.0], [79.7, 4835.0], [79.8, 4835.0], [79.9, 4835.0], [80.0, 4835.0], [80.1, 4939.0], [80.2, 4939.0], [80.3, 4939.0], [80.4, 4939.0], [80.5, 4962.0], [80.6, 4962.0], [80.7, 4962.0], [80.8, 4962.0], [80.9, 4977.0], [81.0, 4977.0], [81.1, 4977.0], [81.2, 4977.0], [81.3, 5015.0], [81.4, 5015.0], [81.5, 5015.0], [81.6, 5015.0], [81.7, 5068.0], [81.8, 5068.0], [81.9, 5068.0], [82.0, 5068.0], [82.1, 5081.0], [82.2, 5081.0], [82.3, 5081.0], [82.4, 5081.0], [82.5, 5087.0], [82.6, 5087.0], [82.7, 5087.0], [82.8, 5087.0], [82.9, 5106.0], [83.0, 5106.0], [83.1, 5106.0], [83.2, 5106.0], [83.3, 5136.0], [83.4, 5136.0], [83.5, 5136.0], [83.6, 5136.0], [83.7, 5220.0], [83.8, 5220.0], [83.9, 5220.0], [84.0, 5220.0], [84.1, 5259.0], [84.2, 5259.0], [84.3, 5259.0], [84.4, 5259.0], [84.5, 5264.0], [84.6, 5264.0], [84.7, 5264.0], [84.8, 5264.0], [84.9, 5265.0], [85.0, 5265.0], [85.1, 5265.0], [85.2, 5265.0], [85.3, 5273.0], [85.4, 5273.0], [85.5, 5273.0], [85.6, 5273.0], [85.7, 5301.0], [85.8, 5301.0], [85.9, 5301.0], [86.0, 5301.0], [86.1, 5406.0], [86.2, 5406.0], [86.3, 5406.0], [86.4, 5406.0], [86.5, 5504.0], [86.6, 5504.0], [86.7, 5504.0], [86.8, 5504.0], [86.9, 5516.0], [87.0, 5516.0], [87.1, 5516.0], [87.2, 5516.0], [87.3, 5536.0], [87.4, 5536.0], [87.5, 5536.0], [87.6, 5536.0], [87.7, 5644.0], [87.8, 5644.0], [87.9, 5644.0], [88.0, 5644.0], [88.1, 5677.0], [88.2, 5677.0], [88.3, 5677.0], [88.4, 5677.0], [88.5, 5707.0], [88.6, 5707.0], [88.7, 5707.0], [88.8, 5707.0], [88.9, 5711.0], [89.0, 5711.0], [89.1, 5711.0], [89.2, 5711.0], [89.3, 5927.0], [89.4, 5927.0], [89.5, 5927.0], [89.6, 5927.0], [89.7, 5938.0], [89.8, 5938.0], [89.9, 5938.0], [90.0, 5938.0], [90.1, 6021.0], [90.2, 6021.0], [90.3, 6021.0], [90.4, 6021.0], [90.5, 6037.0], [90.6, 6037.0], [90.7, 6037.0], [90.8, 6037.0], [90.9, 6121.0], [91.0, 6121.0], [91.1, 6121.0], [91.2, 6121.0], [91.3, 6256.0], [91.4, 6256.0], [91.5, 6256.0], [91.6, 6256.0], [91.7, 6456.0], [91.8, 6456.0], [91.9, 6456.0], [92.0, 6456.0], [92.1, 6461.0], [92.2, 6461.0], [92.3, 6461.0], [92.4, 6461.0], [92.5, 6465.0], [92.6, 6465.0], [92.7, 6465.0], [92.8, 6465.0], [92.9, 6489.0], [93.0, 6489.0], [93.1, 6489.0], [93.2, 6489.0], [93.3, 6506.0], [93.4, 6506.0], [93.5, 6506.0], [93.6, 6506.0], [93.7, 6572.0], [93.8, 6572.0], [93.9, 6572.0], [94.0, 6572.0], [94.1, 6613.0], [94.2, 6613.0], [94.3, 6613.0], [94.4, 6613.0], [94.5, 6615.0], [94.6, 6615.0], [94.7, 6615.0], [94.8, 6615.0], [94.9, 6926.0], [95.0, 6926.0], [95.1, 6926.0], [95.2, 6926.0], [95.3, 6935.0], [95.4, 6935.0], [95.5, 6935.0], [95.6, 6935.0], [95.7, 6938.0], [95.8, 6938.0], [95.9, 6938.0], [96.0, 6938.0], [96.1, 7007.0], [96.2, 7007.0], [96.3, 7007.0], [96.4, 7007.0], [96.5, 7029.0], [96.6, 7029.0], [96.7, 7029.0], [96.8, 7029.0], [96.9, 7088.0], [97.0, 7088.0], [97.1, 7088.0], [97.2, 7088.0], [97.3, 7315.0], [97.4, 7315.0], [97.5, 7315.0], [97.6, 7315.0], [97.7, 7478.0], [97.8, 7478.0], [97.9, 7478.0], [98.0, 7478.0], [98.1, 7709.0], [98.2, 7709.0], [98.3, 7709.0], [98.4, 7709.0], [98.5, 7838.0], [98.6, 7838.0], [98.7, 7838.0], [98.8, 7838.0], [98.9, 7845.0], [99.0, 7845.0], [99.1, 7845.0], [99.2, 7845.0], [99.3, 7876.0], [99.4, 7876.0], [99.5, 7876.0], [99.6, 7876.0], [99.7, 9040.0], [99.8, 9040.0], [99.9, 9040.0], [100.0, 9040.0]], "isOverall": false, "label": "HTTP Reques", "isController": false}], "supportsControllersDiscrimination": true, "maxX": 100.0, "title": "Response Time Percentiles"}},
        getOptions: function() {
            return {
                series: {
                    points: { show: false }
                },
                legend: {
                    noColumns: 2,
                    show: true,
                    container: '#legendResponseTimePercentiles'
                },
                xaxis: {
                    tickDecimals: 1,
                    axisLabel: "Percentiles",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                yaxis: {
                    axisLabel: "Percentile value in ms",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20
                },
                grid: {
                    hoverable: true // IMPORTANT! this is needed for tooltip to
                                    // work
                },
                tooltip: true,
                tooltipOpts: {
                    content: "%s : %x.2 percentile was %y ms"
                },
                selection: { mode: "xy" },
            };
        },
        createGraph: function() {
            var data = this.data;
            var dataset = prepareData(data.result.series, $("#choicesResponseTimePercentiles"));
            var options = this.getOptions();
            prepareOptions(options, data);
            $.plot($("#flotResponseTimesPercentiles"), dataset, options);
            // setup overview
            $.plot($("#overviewResponseTimesPercentiles"), dataset, prepareOverviewOptions(options));
        }
};

/**
 * @param elementId Id of element where we display message
 */
function setEmptyGraph(elementId) {
    $(function() {
        $(elementId).text("No graph series with filter="+seriesFilter);
    });
}

// Response times percentiles
function refreshResponseTimePercentiles() {
    var infos = responseTimePercentilesInfos;
    prepareSeries(infos.data);
    if(infos.data.result.series.length == 0) {
        setEmptyGraph("#bodyResponseTimePercentiles");
        return;
    }
    if (isGraph($("#flotResponseTimesPercentiles"))){
        infos.createGraph();
    } else {
        var choiceContainer = $("#choicesResponseTimePercentiles");
        createLegend(choiceContainer, infos);
        infos.createGraph();
        setGraphZoomable("#flotResponseTimesPercentiles", "#overviewResponseTimesPercentiles");
        $('#bodyResponseTimePercentiles .legendColorBox > div').each(function(i){
            $(this).clone().prependTo(choiceContainer.find("li").eq(i));
        });
    }
}

var responseTimeDistributionInfos = {
        data: {"result": {"minY": 1.0, "minX": 1600.0, "maxY": 19.0, "series": [{"data": [[1600.0, 1.0], [2100.0, 5.0], [2300.0, 5.0], [2200.0, 1.0], [2400.0, 4.0], [2500.0, 3.0], [2600.0, 7.0], [2700.0, 4.0], [2800.0, 11.0], [2900.0, 11.0], [3000.0, 19.0], [3100.0, 11.0], [3300.0, 19.0], [3200.0, 11.0], [3400.0, 13.0], [3500.0, 9.0], [3600.0, 10.0], [3700.0, 5.0], [3800.0, 9.0], [3900.0, 4.0], [4000.0, 7.0], [4300.0, 6.0], [4200.0, 5.0], [4100.0, 7.0], [4500.0, 4.0], [4400.0, 5.0], [4600.0, 3.0], [4800.0, 1.0], [5000.0, 4.0], [4900.0, 3.0], [5100.0, 2.0], [5200.0, 5.0], [5300.0, 1.0], [5400.0, 1.0], [5600.0, 2.0], [5500.0, 3.0], [5700.0, 2.0], [5900.0, 2.0], [6000.0, 2.0], [6100.0, 1.0], [6200.0, 1.0], [6400.0, 4.0], [6500.0, 2.0], [6600.0, 2.0], [6900.0, 3.0], [7000.0, 3.0], [7400.0, 1.0], [7300.0, 1.0], [7800.0, 3.0], [7700.0, 1.0], [9000.0, 1.0]], "isOverall": false, "label": "HTTP Reques", "isController": false}], "supportsControllersDiscrimination": true, "granularity": 100, "maxX": 9000.0, "title": "Response Time Distribution"}},
        getOptions: function() {
            var granularity = this.data.result.granularity;
            return {
                legend: {
                    noColumns: 2,
                    show: true,
                    container: '#legendResponseTimeDistribution'
                },
                xaxis:{
                    axisLabel: "Response times in ms",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                yaxis: {
                    axisLabel: "Number of responses",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                bars : {
                    show: true,
                    barWidth: this.data.result.granularity
                },
                grid: {
                    hoverable: true // IMPORTANT! this is needed for tooltip to
                                    // work
                },
                tooltip: true,
                tooltipOpts: {
                    content: function(label, xval, yval, flotItem){
                        return yval + " responses for " + label + " were between " + xval + " and " + (xval + granularity) + " ms";
                    }
                }
            };
        },
        createGraph: function() {
            var data = this.data;
            var options = this.getOptions();
            prepareOptions(options, data);
            $.plot($("#flotResponseTimeDistribution"), prepareData(data.result.series, $("#choicesResponseTimeDistribution")), options);
        }

};

// Response time distribution
function refreshResponseTimeDistribution() {
    var infos = responseTimeDistributionInfos;
    prepareSeries(infos.data);
    if(infos.data.result.series.length == 0) {
        setEmptyGraph("#bodyResponseTimeDistribution");
        return;
    }
    if (isGraph($("#flotResponseTimeDistribution"))){
        infos.createGraph();
    }else{
        var choiceContainer = $("#choicesResponseTimeDistribution");
        createLegend(choiceContainer, infos);
        infos.createGraph();
        $('#footerResponseTimeDistribution .legendColorBox > div').each(function(i){
            $(this).clone().prependTo(choiceContainer.find("li").eq(i));
        });
    }
};


var syntheticResponseTimeDistributionInfos = {
        data: {"result": {"minY": 250.0, "minX": 2.0, "ticks": [[0, "Requests having \nresponse time <= 500ms"], [1, "Requests having \nresponse time > 500ms and <= 1,500ms"], [2, "Requests having \nresponse time > 1,500ms"], [3, "Requests in error"]], "maxY": 250.0, "series": [{"data": [], "color": "#9ACD32", "isOverall": false, "label": "Requests having \nresponse time <= 500ms", "isController": false}, {"data": [], "color": "yellow", "isOverall": false, "label": "Requests having \nresponse time > 500ms and <= 1,500ms", "isController": false}, {"data": [[2.0, 250.0]], "color": "orange", "isOverall": false, "label": "Requests having \nresponse time > 1,500ms", "isController": false}, {"data": [], "color": "#FF6347", "isOverall": false, "label": "Requests in error", "isController": false}], "supportsControllersDiscrimination": false, "maxX": 2.0, "title": "Synthetic Response Times Distribution"}},
        getOptions: function() {
            return {
                legend: {
                    noColumns: 2,
                    show: true,
                    container: '#legendSyntheticResponseTimeDistribution'
                },
                xaxis:{
                    axisLabel: "Response times ranges",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                    tickLength:0,
                    min:-0.5,
                    max:3.5
                },
                yaxis: {
                    axisLabel: "Number of responses",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                bars : {
                    show: true,
                    align: "center",
                    barWidth: 0.25,
                    fill:.75
                },
                grid: {
                    hoverable: true // IMPORTANT! this is needed for tooltip to
                                    // work
                },
                tooltip: true,
                tooltipOpts: {
                    content: function(label, xval, yval, flotItem){
                        return yval + " " + label;
                    }
                }
            };
        },
        createGraph: function() {
            var data = this.data;
            var options = this.getOptions();
            prepareOptions(options, data);
            options.xaxis.ticks = data.result.ticks;
            $.plot($("#flotSyntheticResponseTimeDistribution"), prepareData(data.result.series, $("#choicesSyntheticResponseTimeDistribution")), options);
        }

};

// Response time distribution
function refreshSyntheticResponseTimeDistribution() {
    var infos = syntheticResponseTimeDistributionInfos;
    prepareSeries(infos.data, true);
    if (isGraph($("#flotSyntheticResponseTimeDistribution"))){
        infos.createGraph();
    }else{
        var choiceContainer = $("#choicesSyntheticResponseTimeDistribution");
        createLegend(choiceContainer, infos);
        infos.createGraph();
        $('#footerSyntheticResponseTimeDistribution .legendColorBox > div').each(function(i){
            $(this).clone().prependTo(choiceContainer.find("li").eq(i));
        });
    }
};

var activeThreadsOverTimeInfos = {
        data: {"result": {"minY": 38.67647058823529, "minX": 1.7879916E12, "maxY": 42.06944444444443, "series": [{"data": [[1.7879916E12, 38.67647058823529], [1.78799166E12, 42.06944444444443]], "isOverall": false, "label": "US-01 Registration Load Test", "isController": false}], "supportsControllersDiscrimination": false, "granularity": 60000, "maxX": 1.78799166E12, "title": "Active Threads Over Time"}},
        getOptions: function() {
            return {
                series: {
                    stack: true,
                    lines: {
                        show: true,
                        fill: true
                    },
                    points: {
                        show: true
                    }
                },
                xaxis: {
                    mode: "time",
                    timeformat: getTimeFormat(this.data.result.granularity),
                    axisLabel: getElapsedTimeLabel(this.data.result.granularity),
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                yaxis: {
                    axisLabel: "Number of active threads",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20
                },
                legend: {
                    noColumns: 6,
                    show: true,
                    container: '#legendActiveThreadsOverTime'
                },
                grid: {
                    hoverable: true // IMPORTANT! this is needed for tooltip to
                                    // work
                },
                selection: {
                    mode: 'xy'
                },
                tooltip: true,
                tooltipOpts: {
                    content: "%s : At %x there were %y active threads"
                }
            };
        },
        createGraph: function() {
            var data = this.data;
            var dataset = prepareData(data.result.series, $("#choicesActiveThreadsOverTime"));
            var options = this.getOptions();
            prepareOptions(options, data);
            $.plot($("#flotActiveThreadsOverTime"), dataset, options);
            // setup overview
            $.plot($("#overviewActiveThreadsOverTime"), dataset, prepareOverviewOptions(options));
        }
};

// Active Threads Over Time
function refreshActiveThreadsOverTime(fixTimestamps) {
    var infos = activeThreadsOverTimeInfos;
    prepareSeries(infos.data);
    if(fixTimestamps) {
        fixTimeStamps(infos.data.result.series, 19800000);
    }
    if(isGraph($("#flotActiveThreadsOverTime"))) {
        infos.createGraph();
    }else{
        var choiceContainer = $("#choicesActiveThreadsOverTime");
        createLegend(choiceContainer, infos);
        infos.createGraph();
        setGraphZoomable("#flotActiveThreadsOverTime", "#overviewActiveThreadsOverTime");
        $('#footerActiveThreadsOverTime .legendColorBox > div').each(function(i){
            $(this).clone().prependTo(choiceContainer.find("li").eq(i));
        });
    }
};

var timeVsThreadsInfos = {
        data: {"result": {"minY": 2112.0, "minX": 1.0, "maxY": 7838.0, "series": [{"data": [[32.0, 5351.0], [33.0, 5539.75], [2.0, 5273.0], [34.0, 4276.25], [35.0, 5307.6], [36.0, 5763.0], [37.0, 4733.8], [38.0, 3682.0], [39.0, 4309.0], [40.0, 5200.857142857142], [41.0, 3616.6666666666665], [43.0, 3843.666666666667], [42.0, 5015.0], [45.0, 3567.0], [47.0, 3393.6666666666665], [46.0, 4303.0], [48.0, 4381.9285714285725], [49.0, 3580.6666666666665], [3.0, 5644.0], [50.0, 3435.2238805970137], [4.0, 2424.0], [5.0, 2239.5], [6.0, 2400.0], [7.0, 2186.0], [8.0, 2335.0], [9.0, 2340.0], [10.0, 2301.0], [11.0, 2112.0], [12.0, 2132.0], [13.0, 2170.0], [14.0, 2937.5], [15.0, 7838.0], [16.0, 2636.0], [1.0, 2311.0], [17.0, 2559.0], [18.0, 5364.5], [19.0, 6344.5], [20.0, 3984.0], [21.0, 4895.666666666667], [22.0, 4109.0], [23.0, 6615.0], [24.0, 4309.5], [25.0, 4579.0], [26.0, 4380.333333333333], [27.0, 6351.666666666667], [29.0, 6481.0], [30.0, 6938.0], [31.0, 5838.0]], "isOverall": false, "label": "HTTP Reques", "isController": false}, {"data": [[41.60799999999999, 3932.5999999999985]], "isOverall": false, "label": "HTTP Reques-Aggregated", "isController": false}], "supportsControllersDiscrimination": true, "maxX": 50.0, "title": "Time VS Threads"}},
        getOptions: function() {
            return {
                series: {
                    lines: {
                        show: true
                    },
                    points: {
                        show: true
                    }
                },
                xaxis: {
                    axisLabel: "Number of active threads",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                yaxis: {
                    axisLabel: "Average response times in ms",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20
                },
                legend: { noColumns: 2,show: true, container: '#legendTimeVsThreads' },
                selection: {
                    mode: 'xy'
                },
                grid: {
                    hoverable: true // IMPORTANT! this is needed for tooltip to work
                },
                tooltip: true,
                tooltipOpts: {
                    content: "%s: At %x.2 active threads, Average response time was %y.2 ms"
                }
            };
        },
        createGraph: function() {
            var data = this.data;
            var dataset = prepareData(data.result.series, $("#choicesTimeVsThreads"));
            var options = this.getOptions();
            prepareOptions(options, data);
            $.plot($("#flotTimesVsThreads"), dataset, options);
            // setup overview
            $.plot($("#overviewTimesVsThreads"), dataset, prepareOverviewOptions(options));
        }
};

// Time vs threads
function refreshTimeVsThreads(){
    var infos = timeVsThreadsInfos;
    prepareSeries(infos.data);
    if(infos.data.result.series.length == 0) {
        setEmptyGraph("#bodyTimeVsThreads");
        return;
    }
    if(isGraph($("#flotTimesVsThreads"))){
        infos.createGraph();
    }else{
        var choiceContainer = $("#choicesTimeVsThreads");
        createLegend(choiceContainer, infos);
        infos.createGraph();
        setGraphZoomable("#flotTimesVsThreads", "#overviewTimesVsThreads");
        $('#footerTimeVsThreads .legendColorBox > div').each(function(i){
            $(this).clone().prependTo(choiceContainer.find("li").eq(i));
        });
    }
};

var bytesThroughputOverTimeInfos = {
        data : {"result": {"minY": 202.3, "minX": 1.7879916E12, "maxY": 1450.8, "series": [{"data": [[1.7879916E12, 202.3], [1.78799166E12, 1285.2166666666667]], "isOverall": false, "label": "Bytes received per second", "isController": false}, {"data": [[1.7879916E12, 228.36666666666667], [1.78799166E12, 1450.8]], "isOverall": false, "label": "Bytes sent per second", "isController": false}], "supportsControllersDiscrimination": false, "granularity": 60000, "maxX": 1.78799166E12, "title": "Bytes Throughput Over Time"}},
        getOptions : function(){
            return {
                series: {
                    lines: {
                        show: true
                    },
                    points: {
                        show: true
                    }
                },
                xaxis: {
                    mode: "time",
                    timeformat: getTimeFormat(this.data.result.granularity),
                    axisLabel: getElapsedTimeLabel(this.data.result.granularity) ,
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                yaxis: {
                    axisLabel: "Bytes / sec",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                legend: {
                    noColumns: 2,
                    show: true,
                    container: '#legendBytesThroughputOverTime'
                },
                selection: {
                    mode: "xy"
                },
                grid: {
                    hoverable: true // IMPORTANT! this is needed for tooltip to
                                    // work
                },
                tooltip: true,
                tooltipOpts: {
                    content: "%s at %x was %y"
                }
            };
        },
        createGraph : function() {
            var data = this.data;
            var dataset = prepareData(data.result.series, $("#choicesBytesThroughputOverTime"));
            var options = this.getOptions();
            prepareOptions(options, data);
            $.plot($("#flotBytesThroughputOverTime"), dataset, options);
            // setup overview
            $.plot($("#overviewBytesThroughputOverTime"), dataset, prepareOverviewOptions(options));
        }
};

// Bytes throughput Over Time
function refreshBytesThroughputOverTime(fixTimestamps) {
    var infos = bytesThroughputOverTimeInfos;
    prepareSeries(infos.data);
    if(fixTimestamps) {
        fixTimeStamps(infos.data.result.series, 19800000);
    }
    if(isGraph($("#flotBytesThroughputOverTime"))){
        infos.createGraph();
    }else{
        var choiceContainer = $("#choicesBytesThroughputOverTime");
        createLegend(choiceContainer, infos);
        infos.createGraph();
        setGraphZoomable("#flotBytesThroughputOverTime", "#overviewBytesThroughputOverTime");
        $('#footerBytesThroughputOverTime .legendColorBox > div').each(function(i){
            $(this).clone().prependTo(choiceContainer.find("li").eq(i));
        });
    }
}

var responseTimesOverTimeInfos = {
        data: {"result": {"minY": 3871.231481481482, "minX": 1.7879916E12, "maxY": 4322.470588235294, "series": [{"data": [[1.7879916E12, 4322.470588235294], [1.78799166E12, 3871.231481481482]], "isOverall": false, "label": "HTTP Reques", "isController": false}], "supportsControllersDiscrimination": true, "granularity": 60000, "maxX": 1.78799166E12, "title": "Response Time Over Time"}},
        getOptions: function(){
            return {
                series: {
                    lines: {
                        show: true
                    },
                    points: {
                        show: true
                    }
                },
                xaxis: {
                    mode: "time",
                    timeformat: getTimeFormat(this.data.result.granularity),
                    axisLabel: getElapsedTimeLabel(this.data.result.granularity),
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                yaxis: {
                    axisLabel: "Average response time in ms",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                legend: {
                    noColumns: 2,
                    show: true,
                    container: '#legendResponseTimesOverTime'
                },
                selection: {
                    mode: 'xy'
                },
                grid: {
                    hoverable: true // IMPORTANT! this is needed for tooltip to
                                    // work
                },
                tooltip: true,
                tooltipOpts: {
                    content: "%s : at %x Average response time was %y ms"
                }
            };
        },
        createGraph: function() {
            var data = this.data;
            var dataset = prepareData(data.result.series, $("#choicesResponseTimesOverTime"));
            var options = this.getOptions();
            prepareOptions(options, data);
            $.plot($("#flotResponseTimesOverTime"), dataset, options);
            // setup overview
            $.plot($("#overviewResponseTimesOverTime"), dataset, prepareOverviewOptions(options));
        }
};

// Response Times Over Time
function refreshResponseTimeOverTime(fixTimestamps) {
    var infos = responseTimesOverTimeInfos;
    prepareSeries(infos.data);
    if(infos.data.result.series.length == 0) {
        setEmptyGraph("#bodyResponseTimeOverTime");
        return;
    }
    if(fixTimestamps) {
        fixTimeStamps(infos.data.result.series, 19800000);
    }
    if(isGraph($("#flotResponseTimesOverTime"))){
        infos.createGraph();
    }else{
        var choiceContainer = $("#choicesResponseTimesOverTime");
        createLegend(choiceContainer, infos);
        infos.createGraph();
        setGraphZoomable("#flotResponseTimesOverTime", "#overviewResponseTimesOverTime");
        $('#footerResponseTimesOverTime .legendColorBox > div').each(function(i){
            $(this).clone().prependTo(choiceContainer.find("li").eq(i));
        });
    }
};

var latenciesOverTimeInfos = {
        data: {"result": {"minY": 3871.157407407408, "minX": 1.7879916E12, "maxY": 4321.941176470588, "series": [{"data": [[1.7879916E12, 4321.941176470588], [1.78799166E12, 3871.157407407408]], "isOverall": false, "label": "HTTP Reques", "isController": false}], "supportsControllersDiscrimination": true, "granularity": 60000, "maxX": 1.78799166E12, "title": "Latencies Over Time"}},
        getOptions: function() {
            return {
                series: {
                    lines: {
                        show: true
                    },
                    points: {
                        show: true
                    }
                },
                xaxis: {
                    mode: "time",
                    timeformat: getTimeFormat(this.data.result.granularity),
                    axisLabel: getElapsedTimeLabel(this.data.result.granularity),
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                yaxis: {
                    axisLabel: "Average response latencies in ms",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                legend: {
                    noColumns: 2,
                    show: true,
                    container: '#legendLatenciesOverTime'
                },
                selection: {
                    mode: 'xy'
                },
                grid: {
                    hoverable: true // IMPORTANT! this is needed for tooltip to
                                    // work
                },
                tooltip: true,
                tooltipOpts: {
                    content: "%s : at %x Average latency was %y ms"
                }
            };
        },
        createGraph: function () {
            var data = this.data;
            var dataset = prepareData(data.result.series, $("#choicesLatenciesOverTime"));
            var options = this.getOptions();
            prepareOptions(options, data);
            $.plot($("#flotLatenciesOverTime"), dataset, options);
            // setup overview
            $.plot($("#overviewLatenciesOverTime"), dataset, prepareOverviewOptions(options));
        }
};

// Latencies Over Time
function refreshLatenciesOverTime(fixTimestamps) {
    var infos = latenciesOverTimeInfos;
    prepareSeries(infos.data);
    if(infos.data.result.series.length == 0) {
        setEmptyGraph("#bodyLatenciesOverTime");
        return;
    }
    if(fixTimestamps) {
        fixTimeStamps(infos.data.result.series, 19800000);
    }
    if(isGraph($("#flotLatenciesOverTime"))) {
        infos.createGraph();
    }else {
        var choiceContainer = $("#choicesLatenciesOverTime");
        createLegend(choiceContainer, infos);
        infos.createGraph();
        setGraphZoomable("#flotLatenciesOverTime", "#overviewLatenciesOverTime");
        $('#footerLatenciesOverTime .legendColorBox > div').each(function(i){
            $(this).clone().prependTo(choiceContainer.find("li").eq(i));
        });
    }
};

var connectTimeOverTimeInfos = {
        data: {"result": {"minY": 0.15277777777777785, "minX": 1.7879916E12, "maxY": 5.7058823529411775, "series": [{"data": [[1.7879916E12, 5.7058823529411775], [1.78799166E12, 0.15277777777777785]], "isOverall": false, "label": "HTTP Reques", "isController": false}], "supportsControllersDiscrimination": true, "granularity": 60000, "maxX": 1.78799166E12, "title": "Connect Time Over Time"}},
        getOptions: function() {
            return {
                series: {
                    lines: {
                        show: true
                    },
                    points: {
                        show: true
                    }
                },
                xaxis: {
                    mode: "time",
                    timeformat: getTimeFormat(this.data.result.granularity),
                    axisLabel: getConnectTimeLabel(this.data.result.granularity),
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                yaxis: {
                    axisLabel: "Average Connect Time in ms",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                legend: {
                    noColumns: 2,
                    show: true,
                    container: '#legendConnectTimeOverTime'
                },
                selection: {
                    mode: 'xy'
                },
                grid: {
                    hoverable: true // IMPORTANT! this is needed for tooltip to
                                    // work
                },
                tooltip: true,
                tooltipOpts: {
                    content: "%s : at %x Average connect time was %y ms"
                }
            };
        },
        createGraph: function () {
            var data = this.data;
            var dataset = prepareData(data.result.series, $("#choicesConnectTimeOverTime"));
            var options = this.getOptions();
            prepareOptions(options, data);
            $.plot($("#flotConnectTimeOverTime"), dataset, options);
            // setup overview
            $.plot($("#overviewConnectTimeOverTime"), dataset, prepareOverviewOptions(options));
        }
};

// Connect Time Over Time
function refreshConnectTimeOverTime(fixTimestamps) {
    var infos = connectTimeOverTimeInfos;
    prepareSeries(infos.data);
    if(infos.data.result.series.length == 0) {
        setEmptyGraph("#bodyConnectTimeOverTime");
        return;
    }
    if(fixTimestamps) {
        fixTimeStamps(infos.data.result.series, 19800000);
    }
    if(isGraph($("#flotConnectTimeOverTime"))) {
        infos.createGraph();
    }else {
        var choiceContainer = $("#choicesConnectTimeOverTime");
        createLegend(choiceContainer, infos);
        infos.createGraph();
        setGraphZoomable("#flotConnectTimeOverTime", "#overviewConnectTimeOverTime");
        $('#footerConnectTimeOverTime .legendColorBox > div').each(function(i){
            $(this).clone().prependTo(choiceContainer.find("li").eq(i));
        });
    }
};

var responseTimePercentilesOverTimeInfos = {
        data: {"result": {"minY": 1681.0, "minX": 1.7879916E12, "maxY": 9040.0, "series": [{"data": [[1.7879916E12, 7876.0], [1.78799166E12, 9040.0]], "isOverall": false, "label": "Max", "isController": false}, {"data": [[1.7879916E12, 2553.0], [1.78799166E12, 1681.0]], "isOverall": false, "label": "Min", "isController": false}, {"data": [[1.7879916E12, 6720.5], [1.78799166E12, 5775.800000000003]], "isOverall": false, "label": "90th percentile", "isController": false}, {"data": [[1.7879916E12, 7876.0], [1.78799166E12, 7843.8099999999995]], "isOverall": false, "label": "99th percentile", "isController": false}, {"data": [[1.7879916E12, 4165.5], [1.78799166E12, 3484.5]], "isOverall": false, "label": "Median", "isController": false}, {"data": [[1.7879916E12, 7577.5], [1.78799166E12, 6661.649999999993]], "isOverall": false, "label": "95th percentile", "isController": false}], "supportsControllersDiscrimination": false, "granularity": 60000, "maxX": 1.78799166E12, "title": "Response Time Percentiles Over Time (successful requests only)"}},
        getOptions: function() {
            return {
                series: {
                    lines: {
                        show: true,
                        fill: true
                    },
                    points: {
                        show: true
                    }
                },
                xaxis: {
                    mode: "time",
                    timeformat: getTimeFormat(this.data.result.granularity),
                    axisLabel: getElapsedTimeLabel(this.data.result.granularity),
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                yaxis: {
                    axisLabel: "Response Time in ms",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                legend: {
                    noColumns: 2,
                    show: true,
                    container: '#legendResponseTimePercentilesOverTime'
                },
                selection: {
                    mode: 'xy'
                },
                grid: {
                    hoverable: true // IMPORTANT! this is needed for tooltip to
                                    // work
                },
                tooltip: true,
                tooltipOpts: {
                    content: "%s : at %x Response time was %y ms"
                }
            };
        },
        createGraph: function () {
            var data = this.data;
            var dataset = prepareData(data.result.series, $("#choicesResponseTimePercentilesOverTime"));
            var options = this.getOptions();
            prepareOptions(options, data);
            $.plot($("#flotResponseTimePercentilesOverTime"), dataset, options);
            // setup overview
            $.plot($("#overviewResponseTimePercentilesOverTime"), dataset, prepareOverviewOptions(options));
        }
};

// Response Time Percentiles Over Time
function refreshResponseTimePercentilesOverTime(fixTimestamps) {
    var infos = responseTimePercentilesOverTimeInfos;
    prepareSeries(infos.data);
    if(fixTimestamps) {
        fixTimeStamps(infos.data.result.series, 19800000);
    }
    if(isGraph($("#flotResponseTimePercentilesOverTime"))) {
        infos.createGraph();
    }else {
        var choiceContainer = $("#choicesResponseTimePercentilesOverTime");
        createLegend(choiceContainer, infos);
        infos.createGraph();
        setGraphZoomable("#flotResponseTimePercentilesOverTime", "#overviewResponseTimePercentilesOverTime");
        $('#footerResponseTimePercentilesOverTime .legendColorBox > div').each(function(i){
            $(this).clone().prependTo(choiceContainer.find("li").eq(i));
        });
    }
};


var responseTimeVsRequestInfos = {
    data: {"result": {"minY": 2251.5, "minX": 1.0, "maxY": 5273.0, "series": [{"data": [[2.0, 4796.0], [9.0, 3417.0], [10.0, 4654.0], [12.0, 5153.5], [3.0, 5273.0], [13.0, 4176.0], [14.0, 3702.5], [16.0, 3015.5], [1.0, 2251.5], [17.0, 3185.0], [18.0, 3686.5], [5.0, 3677.5], [22.0, 3119.5], [6.0, 2874.5], [7.0, 3569.0]], "isOverall": false, "label": "Successes", "isController": false}], "supportsControllersDiscrimination": false, "granularity": 1000, "maxX": 22.0, "title": "Response Time Vs Request"}},
    getOptions: function() {
        return {
            series: {
                lines: {
                    show: false
                },
                points: {
                    show: true
                }
            },
            xaxis: {
                axisLabel: "Global number of requests per second",
                axisLabelUseCanvas: true,
                axisLabelFontSizePixels: 12,
                axisLabelFontFamily: 'Verdana, Arial',
                axisLabelPadding: 20,
            },
            yaxis: {
                axisLabel: "Median Response Time in ms",
                axisLabelUseCanvas: true,
                axisLabelFontSizePixels: 12,
                axisLabelFontFamily: 'Verdana, Arial',
                axisLabelPadding: 20,
            },
            legend: {
                noColumns: 2,
                show: true,
                container: '#legendResponseTimeVsRequest'
            },
            selection: {
                mode: 'xy'
            },
            grid: {
                hoverable: true // IMPORTANT! this is needed for tooltip to work
            },
            tooltip: true,
            tooltipOpts: {
                content: "%s : Median response time at %x req/s was %y ms"
            },
            colors: ["#9ACD32", "#FF6347"]
        };
    },
    createGraph: function () {
        var data = this.data;
        var dataset = prepareData(data.result.series, $("#choicesResponseTimeVsRequest"));
        var options = this.getOptions();
        prepareOptions(options, data);
        $.plot($("#flotResponseTimeVsRequest"), dataset, options);
        // setup overview
        $.plot($("#overviewResponseTimeVsRequest"), dataset, prepareOverviewOptions(options));

    }
};

// Response Time vs Request
function refreshResponseTimeVsRequest() {
    var infos = responseTimeVsRequestInfos;
    prepareSeries(infos.data);
    if (isGraph($("#flotResponseTimeVsRequest"))){
        infos.createGraph();
    }else{
        var choiceContainer = $("#choicesResponseTimeVsRequest");
        createLegend(choiceContainer, infos);
        infos.createGraph();
        setGraphZoomable("#flotResponseTimeVsRequest", "#overviewResponseTimeVsRequest");
        $('#footerResponseRimeVsRequest .legendColorBox > div').each(function(i){
            $(this).clone().prependTo(choiceContainer.find("li").eq(i));
        });
    }
};


var latenciesVsRequestInfos = {
    data: {"result": {"minY": 2251.5, "minX": 1.0, "maxY": 5273.0, "series": [{"data": [[2.0, 4788.5], [9.0, 3417.0], [10.0, 4654.0], [12.0, 5153.5], [3.0, 5273.0], [13.0, 4176.0], [14.0, 3702.5], [16.0, 3015.5], [1.0, 2251.5], [17.0, 3185.0], [18.0, 3686.5], [5.0, 3677.5], [22.0, 3119.5], [6.0, 2874.5], [7.0, 3569.0]], "isOverall": false, "label": "Successes", "isController": false}], "supportsControllersDiscrimination": false, "granularity": 1000, "maxX": 22.0, "title": "Latencies Vs Request"}},
    getOptions: function() {
        return{
            series: {
                lines: {
                    show: false
                },
                points: {
                    show: true
                }
            },
            xaxis: {
                axisLabel: "Global number of requests per second",
                axisLabelUseCanvas: true,
                axisLabelFontSizePixels: 12,
                axisLabelFontFamily: 'Verdana, Arial',
                axisLabelPadding: 20,
            },
            yaxis: {
                axisLabel: "Median Latency in ms",
                axisLabelUseCanvas: true,
                axisLabelFontSizePixels: 12,
                axisLabelFontFamily: 'Verdana, Arial',
                axisLabelPadding: 20,
            },
            legend: { noColumns: 2,show: true, container: '#legendLatencyVsRequest' },
            selection: {
                mode: 'xy'
            },
            grid: {
                hoverable: true // IMPORTANT! this is needed for tooltip to work
            },
            tooltip: true,
            tooltipOpts: {
                content: "%s : Median Latency time at %x req/s was %y ms"
            },
            colors: ["#9ACD32", "#FF6347"]
        };
    },
    createGraph: function () {
        var data = this.data;
        var dataset = prepareData(data.result.series, $("#choicesLatencyVsRequest"));
        var options = this.getOptions();
        prepareOptions(options, data);
        $.plot($("#flotLatenciesVsRequest"), dataset, options);
        // setup overview
        $.plot($("#overviewLatenciesVsRequest"), dataset, prepareOverviewOptions(options));
    }
};

// Latencies vs Request
function refreshLatenciesVsRequest() {
        var infos = latenciesVsRequestInfos;
        prepareSeries(infos.data);
        if(isGraph($("#flotLatenciesVsRequest"))){
            infos.createGraph();
        }else{
            var choiceContainer = $("#choicesLatencyVsRequest");
            createLegend(choiceContainer, infos);
            infos.createGraph();
            setGraphZoomable("#flotLatenciesVsRequest", "#overviewLatenciesVsRequest");
            $('#footerLatenciesVsRequest .legendColorBox > div').each(function(i){
                $(this).clone().prependTo(choiceContainer.find("li").eq(i));
            });
        }
};

var hitsPerSecondInfos = {
        data: {"result": {"minY": 1.3666666666666667, "minX": 1.7879916E12, "maxY": 2.8, "series": [{"data": [[1.7879916E12, 1.3666666666666667], [1.78799166E12, 2.8]], "isOverall": false, "label": "hitsPerSecond", "isController": false}], "supportsControllersDiscrimination": false, "granularity": 60000, "maxX": 1.78799166E12, "title": "Hits Per Second"}},
        getOptions: function() {
            return {
                series: {
                    lines: {
                        show: true
                    },
                    points: {
                        show: true
                    }
                },
                xaxis: {
                    mode: "time",
                    timeformat: getTimeFormat(this.data.result.granularity),
                    axisLabel: getElapsedTimeLabel(this.data.result.granularity),
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                yaxis: {
                    axisLabel: "Number of hits / sec",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20
                },
                legend: {
                    noColumns: 2,
                    show: true,
                    container: "#legendHitsPerSecond"
                },
                selection: {
                    mode : 'xy'
                },
                grid: {
                    hoverable: true // IMPORTANT! this is needed for tooltip to
                                    // work
                },
                tooltip: true,
                tooltipOpts: {
                    content: "%s at %x was %y.2 hits/sec"
                }
            };
        },
        createGraph: function createGraph() {
            var data = this.data;
            var dataset = prepareData(data.result.series, $("#choicesHitsPerSecond"));
            var options = this.getOptions();
            prepareOptions(options, data);
            $.plot($("#flotHitsPerSecond"), dataset, options);
            // setup overview
            $.plot($("#overviewHitsPerSecond"), dataset, prepareOverviewOptions(options));
        }
};

// Hits per second
function refreshHitsPerSecond(fixTimestamps) {
    var infos = hitsPerSecondInfos;
    prepareSeries(infos.data);
    if(fixTimestamps) {
        fixTimeStamps(infos.data.result.series, 19800000);
    }
    if (isGraph($("#flotHitsPerSecond"))){
        infos.createGraph();
    }else{
        var choiceContainer = $("#choicesHitsPerSecond");
        createLegend(choiceContainer, infos);
        infos.createGraph();
        setGraphZoomable("#flotHitsPerSecond", "#overviewHitsPerSecond");
        $('#footerHitsPerSecond .legendColorBox > div').each(function(i){
            $(this).clone().prependTo(choiceContainer.find("li").eq(i));
        });
    }
}

var codesPerSecondInfos = {
        data: {"result": {"minY": 0.5666666666666667, "minX": 1.7879916E12, "maxY": 3.6, "series": [{"data": [[1.7879916E12, 0.5666666666666667], [1.78799166E12, 3.6]], "isOverall": false, "label": "201", "isController": false}], "supportsControllersDiscrimination": false, "granularity": 60000, "maxX": 1.78799166E12, "title": "Codes Per Second"}},
        getOptions: function(){
            return {
                series: {
                    lines: {
                        show: true
                    },
                    points: {
                        show: true
                    }
                },
                xaxis: {
                    mode: "time",
                    timeformat: getTimeFormat(this.data.result.granularity),
                    axisLabel: getElapsedTimeLabel(this.data.result.granularity),
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                yaxis: {
                    axisLabel: "Number of responses / sec",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                legend: {
                    noColumns: 2,
                    show: true,
                    container: "#legendCodesPerSecond"
                },
                selection: {
                    mode: 'xy'
                },
                grid: {
                    hoverable: true // IMPORTANT! this is needed for tooltip to
                                    // work
                },
                tooltip: true,
                tooltipOpts: {
                    content: "Number of Response Codes %s at %x was %y.2 responses / sec"
                }
            };
        },
    createGraph: function() {
        var data = this.data;
        var dataset = prepareData(data.result.series, $("#choicesCodesPerSecond"));
        var options = this.getOptions();
        prepareOptions(options, data);
        $.plot($("#flotCodesPerSecond"), dataset, options);
        // setup overview
        $.plot($("#overviewCodesPerSecond"), dataset, prepareOverviewOptions(options));
    }
};

// Codes per second
function refreshCodesPerSecond(fixTimestamps) {
    var infos = codesPerSecondInfos;
    prepareSeries(infos.data);
    if(fixTimestamps) {
        fixTimeStamps(infos.data.result.series, 19800000);
    }
    if(isGraph($("#flotCodesPerSecond"))){
        infos.createGraph();
    }else{
        var choiceContainer = $("#choicesCodesPerSecond");
        createLegend(choiceContainer, infos);
        infos.createGraph();
        setGraphZoomable("#flotCodesPerSecond", "#overviewCodesPerSecond");
        $('#footerCodesPerSecond .legendColorBox > div').each(function(i){
            $(this).clone().prependTo(choiceContainer.find("li").eq(i));
        });
    }
};

var transactionsPerSecondInfos = {
        data: {"result": {"minY": 0.5666666666666667, "minX": 1.7879916E12, "maxY": 3.6, "series": [{"data": [[1.7879916E12, 0.5666666666666667], [1.78799166E12, 3.6]], "isOverall": false, "label": "HTTP Reques-success", "isController": false}], "supportsControllersDiscrimination": true, "granularity": 60000, "maxX": 1.78799166E12, "title": "Transactions Per Second"}},
        getOptions: function(){
            return {
                series: {
                    lines: {
                        show: true
                    },
                    points: {
                        show: true
                    }
                },
                xaxis: {
                    mode: "time",
                    timeformat: getTimeFormat(this.data.result.granularity),
                    axisLabel: getElapsedTimeLabel(this.data.result.granularity),
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                yaxis: {
                    axisLabel: "Number of transactions / sec",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20
                },
                legend: {
                    noColumns: 2,
                    show: true,
                    container: "#legendTransactionsPerSecond"
                },
                selection: {
                    mode: 'xy'
                },
                grid: {
                    hoverable: true // IMPORTANT! this is needed for tooltip to
                                    // work
                },
                tooltip: true,
                tooltipOpts: {
                    content: "%s at %x was %y transactions / sec"
                }
            };
        },
    createGraph: function () {
        var data = this.data;
        var dataset = prepareData(data.result.series, $("#choicesTransactionsPerSecond"));
        var options = this.getOptions();
        prepareOptions(options, data);
        $.plot($("#flotTransactionsPerSecond"), dataset, options);
        // setup overview
        $.plot($("#overviewTransactionsPerSecond"), dataset, prepareOverviewOptions(options));
    }
};

// Transactions per second
function refreshTransactionsPerSecond(fixTimestamps) {
    var infos = transactionsPerSecondInfos;
    prepareSeries(infos.data);
    if(infos.data.result.series.length == 0) {
        setEmptyGraph("#bodyTransactionsPerSecond");
        return;
    }
    if(fixTimestamps) {
        fixTimeStamps(infos.data.result.series, 19800000);
    }
    if(isGraph($("#flotTransactionsPerSecond"))){
        infos.createGraph();
    }else{
        var choiceContainer = $("#choicesTransactionsPerSecond");
        createLegend(choiceContainer, infos);
        infos.createGraph();
        setGraphZoomable("#flotTransactionsPerSecond", "#overviewTransactionsPerSecond");
        $('#footerTransactionsPerSecond .legendColorBox > div').each(function(i){
            $(this).clone().prependTo(choiceContainer.find("li").eq(i));
        });
    }
};

var totalTPSInfos = {
        data: {"result": {"minY": 0.5666666666666667, "minX": 1.7879916E12, "maxY": 3.6, "series": [{"data": [[1.7879916E12, 0.5666666666666667], [1.78799166E12, 3.6]], "isOverall": false, "label": "Transaction-success", "isController": false}, {"data": [], "isOverall": false, "label": "Transaction-failure", "isController": false}], "supportsControllersDiscrimination": true, "granularity": 60000, "maxX": 1.78799166E12, "title": "Total Transactions Per Second"}},
        getOptions: function(){
            return {
                series: {
                    lines: {
                        show: true
                    },
                    points: {
                        show: true
                    }
                },
                xaxis: {
                    mode: "time",
                    timeformat: getTimeFormat(this.data.result.granularity),
                    axisLabel: getElapsedTimeLabel(this.data.result.granularity),
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20,
                },
                yaxis: {
                    axisLabel: "Number of transactions / sec",
                    axisLabelUseCanvas: true,
                    axisLabelFontSizePixels: 12,
                    axisLabelFontFamily: 'Verdana, Arial',
                    axisLabelPadding: 20
                },
                legend: {
                    noColumns: 2,
                    show: true,
                    container: "#legendTotalTPS"
                },
                selection: {
                    mode: 'xy'
                },
                grid: {
                    hoverable: true // IMPORTANT! this is needed for tooltip to
                                    // work
                },
                tooltip: true,
                tooltipOpts: {
                    content: "%s at %x was %y transactions / sec"
                },
                colors: ["#9ACD32", "#FF6347"]
            };
        },
    createGraph: function () {
        var data = this.data;
        var dataset = prepareData(data.result.series, $("#choicesTotalTPS"));
        var options = this.getOptions();
        prepareOptions(options, data);
        $.plot($("#flotTotalTPS"), dataset, options);
        // setup overview
        $.plot($("#overviewTotalTPS"), dataset, prepareOverviewOptions(options));
    }
};

// Total Transactions per second
function refreshTotalTPS(fixTimestamps) {
    var infos = totalTPSInfos;
    // We want to ignore seriesFilter
    prepareSeries(infos.data, false, true);
    if(fixTimestamps) {
        fixTimeStamps(infos.data.result.series, 19800000);
    }
    if(isGraph($("#flotTotalTPS"))){
        infos.createGraph();
    }else{
        var choiceContainer = $("#choicesTotalTPS");
        createLegend(choiceContainer, infos);
        infos.createGraph();
        setGraphZoomable("#flotTotalTPS", "#overviewTotalTPS");
        $('#footerTotalTPS .legendColorBox > div').each(function(i){
            $(this).clone().prependTo(choiceContainer.find("li").eq(i));
        });
    }
};

// Collapse the graph matching the specified DOM element depending the collapsed
// status
function collapse(elem, collapsed){
    if(collapsed){
        $(elem).parent().find(".fa-chevron-up").removeClass("fa-chevron-up").addClass("fa-chevron-down");
    } else {
        $(elem).parent().find(".fa-chevron-down").removeClass("fa-chevron-down").addClass("fa-chevron-up");
        if (elem.id == "bodyBytesThroughputOverTime") {
            if (isGraph($(elem).find('.flot-chart-content')) == false) {
                refreshBytesThroughputOverTime(true);
            }
            document.location.href="#bytesThroughputOverTime";
        } else if (elem.id == "bodyLatenciesOverTime") {
            if (isGraph($(elem).find('.flot-chart-content')) == false) {
                refreshLatenciesOverTime(true);
            }
            document.location.href="#latenciesOverTime";
        } else if (elem.id == "bodyCustomGraph") {
            if (isGraph($(elem).find('.flot-chart-content')) == false) {
                refreshCustomGraph(true);
            }
            document.location.href="#responseCustomGraph";
        } else if (elem.id == "bodyConnectTimeOverTime") {
            if (isGraph($(elem).find('.flot-chart-content')) == false) {
                refreshConnectTimeOverTime(true);
            }
            document.location.href="#connectTimeOverTime";
        } else if (elem.id == "bodyResponseTimePercentilesOverTime") {
            if (isGraph($(elem).find('.flot-chart-content')) == false) {
                refreshResponseTimePercentilesOverTime(true);
            }
            document.location.href="#responseTimePercentilesOverTime";
        } else if (elem.id == "bodyResponseTimeDistribution") {
            if (isGraph($(elem).find('.flot-chart-content')) == false) {
                refreshResponseTimeDistribution();
            }
            document.location.href="#responseTimeDistribution" ;
        } else if (elem.id == "bodySyntheticResponseTimeDistribution") {
            if (isGraph($(elem).find('.flot-chart-content')) == false) {
                refreshSyntheticResponseTimeDistribution();
            }
            document.location.href="#syntheticResponseTimeDistribution" ;
        } else if (elem.id == "bodyActiveThreadsOverTime") {
            if (isGraph($(elem).find('.flot-chart-content')) == false) {
                refreshActiveThreadsOverTime(true);
            }
            document.location.href="#activeThreadsOverTime";
        } else if (elem.id == "bodyTimeVsThreads") {
            if (isGraph($(elem).find('.flot-chart-content')) == false) {
                refreshTimeVsThreads();
            }
            document.location.href="#timeVsThreads" ;
        } else if (elem.id == "bodyCodesPerSecond") {
            if (isGraph($(elem).find('.flot-chart-content')) == false) {
                refreshCodesPerSecond(true);
            }
            document.location.href="#codesPerSecond";
        } else if (elem.id == "bodyTransactionsPerSecond") {
            if (isGraph($(elem).find('.flot-chart-content')) == false) {
                refreshTransactionsPerSecond(true);
            }
            document.location.href="#transactionsPerSecond";
        } else if (elem.id == "bodyTotalTPS") {
            if (isGraph($(elem).find('.flot-chart-content')) == false) {
                refreshTotalTPS(true);
            }
            document.location.href="#totalTPS";
        } else if (elem.id == "bodyResponseTimeVsRequest") {
            if (isGraph($(elem).find('.flot-chart-content')) == false) {
                refreshResponseTimeVsRequest();
            }
            document.location.href="#responseTimeVsRequest";
        } else if (elem.id == "bodyLatenciesVsRequest") {
            if (isGraph($(elem).find('.flot-chart-content')) == false) {
                refreshLatenciesVsRequest();
            }
            document.location.href="#latencyVsRequest";
        }
    }
}

/*
 * Activates or deactivates all series of the specified graph (represented by id parameter)
 * depending on checked argument.
 */
function toggleAll(id, checked){
    var placeholder = document.getElementById(id);

    var cases = $(placeholder).find(':checkbox');
    cases.prop('checked', checked);
    $(cases).parent().children().children().toggleClass("legend-disabled", !checked);

    var choiceContainer;
    if ( id == "choicesBytesThroughputOverTime"){
        choiceContainer = $("#choicesBytesThroughputOverTime");
        refreshBytesThroughputOverTime(false);
    } else if(id == "choicesResponseTimesOverTime"){
        choiceContainer = $("#choicesResponseTimesOverTime");
        refreshResponseTimeOverTime(false);
    }else if(id == "choicesResponseCustomGraph"){
        choiceContainer = $("#choicesResponseCustomGraph");
        refreshCustomGraph(false);
    } else if ( id == "choicesLatenciesOverTime"){
        choiceContainer = $("#choicesLatenciesOverTime");
        refreshLatenciesOverTime(false);
    } else if ( id == "choicesConnectTimeOverTime"){
        choiceContainer = $("#choicesConnectTimeOverTime");
        refreshConnectTimeOverTime(false);
    } else if ( id == "choicesResponseTimePercentilesOverTime"){
        choiceContainer = $("#choicesResponseTimePercentilesOverTime");
        refreshResponseTimePercentilesOverTime(false);
    } else if ( id == "choicesResponseTimePercentiles"){
        choiceContainer = $("#choicesResponseTimePercentiles");
        refreshResponseTimePercentiles();
    } else if(id == "choicesActiveThreadsOverTime"){
        choiceContainer = $("#choicesActiveThreadsOverTime");
        refreshActiveThreadsOverTime(false);
    } else if ( id == "choicesTimeVsThreads"){
        choiceContainer = $("#choicesTimeVsThreads");
        refreshTimeVsThreads();
    } else if ( id == "choicesSyntheticResponseTimeDistribution"){
        choiceContainer = $("#choicesSyntheticResponseTimeDistribution");
        refreshSyntheticResponseTimeDistribution();
    } else if ( id == "choicesResponseTimeDistribution"){
        choiceContainer = $("#choicesResponseTimeDistribution");
        refreshResponseTimeDistribution();
    } else if ( id == "choicesHitsPerSecond"){
        choiceContainer = $("#choicesHitsPerSecond");
        refreshHitsPerSecond(false);
    } else if(id == "choicesCodesPerSecond"){
        choiceContainer = $("#choicesCodesPerSecond");
        refreshCodesPerSecond(false);
    } else if ( id == "choicesTransactionsPerSecond"){
        choiceContainer = $("#choicesTransactionsPerSecond");
        refreshTransactionsPerSecond(false);
    } else if ( id == "choicesTotalTPS"){
        choiceContainer = $("#choicesTotalTPS");
        refreshTotalTPS(false);
    } else if ( id == "choicesResponseTimeVsRequest"){
        choiceContainer = $("#choicesResponseTimeVsRequest");
        refreshResponseTimeVsRequest();
    } else if ( id == "choicesLatencyVsRequest"){
        choiceContainer = $("#choicesLatencyVsRequest");
        refreshLatenciesVsRequest();
    }
    var color = checked ? "black" : "#818181";
    if(choiceContainer != null) {
        choiceContainer.find("label").each(function(){
            this.style.color = color;
        });
    }
}

