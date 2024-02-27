using CSScripting;
using Flurl.Util;
using Hungsum.MyAliyun;
using Hungsum.Sys.Utility;
using ICSharpCode.SharpZipLib.Zip;
using NPOI.SS.Util;
using PListNet;
using System.Drawing.Drawing2D;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;


namespace exam
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {

                // new TempExam().Gen();

                // return;
                

                Console.Write("请输入姓名[0 翟静逸 1 翟绍祖]：");
                string name = Console.ReadLine() == "1" ? "翟绍祖" : "翟静逸";

                //
                Console.Write("请输入题库：");
                string[] files = Regex.Split(Console.ReadLine() ?? "" , @"\s+").Select(r=> $"Assets/Dict/{r}.txt").ToArray();

                foreach (string file in files) 
                {
                    if (!File.Exists(file))
                    {
                        throw new HsException($"文件【{file}】不存在");
                    }
                }


                //
                Console.Write("需要生成的测试数量[5]：");
                int examCount = Console.ReadLine().TransInt(5);

                //
                Console.Write("每个测试的题目数[25]：");
                int countPerExam = Console.ReadLine().TransInt(25);

                Console.Write("测试类型(0 全部 1 听英语写汉语 2 听汉语写英语])：");
                int lx = Console.ReadLine().TransInt(0);

                var exam = new HearingExam(name, files, examCount, countPerExam);

                var tasks = new List<Task>();

                if (lx == 1)
                {
                    tasks.Add(exam.Gen(HearingExam.ELx.听英语写汉语));
                } else if (lx == 2)
                {
                    tasks.Add(exam.Gen(HearingExam.ELx.听英语写汉语));
                }
                else
                {
                    tasks.Add(exam.Gen(HearingExam.ELx.听英语写汉语));
                    tasks.Add(exam.Gen(HearingExam.ELx.听汉语写英语));
                }

                Task.WaitAll(tasks.ToArray());

                Console.WriteLine("全部处理完毕。");


            }
            catch (Exception ex) 
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }


    public abstract class Exam
    {
        private AliyunUtil? _util;

        protected AliyunUtil util
        {
            get
            {
                if (_util == null)
                {
                    var xConfig = XElement.Parse(File.ReadAllText("Config.xml"));

                    string accessKey = xConfig.GetStringValue("AccessKey");

                    string secret = xConfig.GetStringValue("Secret");
                    
                    string appKey = xConfig.GetStringValue("AppKey");

                    _util = new AliyunUtil(accessKey, secret, appKey);
                }
                
                return _util;
            }
        }

        protected async Task<byte[]> getTtsAsync(string text, string voice , int speedRate = 0)
        {
            var req = new AliyunUtil.YyhcReqest(text, voice, speechRate:speedRate);

            string path = "Cache";

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            string cacheFile = Path.Combine(path, req.ToString());

            if (File.Exists(cacheFile))
            {
                return await File.ReadAllBytesAsync(cacheFile);
            } else
            {
                var bs = await util.GetYyhcResultAsync(req);

                await File.WriteAllBytesAsync(cacheFile, bs);

                return bs;
            }
        }


    }

    public class TempExam : Exam
    {
        public TempExam()
        {

        }

        public void Gen()
        {
            string text_of_frank_brown = "<speak>I don't have a soccer ball , but my brother Alan does. We go to the same school and we love soccer. We play it at school with out friends. It's relaxing.<break time='2s' /></speak>";

            string text_of_gina_smith = "<speak>Yes,I do. I have two soccer balls, three volleyballs, four basketballs and five baseballs and bats, I love sports, but I dont't play them --- I only watch them on TV.<break time='2s' /></speak>";

            string text_of_wang_wei = "<speak>No, I don't. Soccer is difficult. I like ping-pong. It's easy for me. I have three ping-pong balls and two ping-pong bats. After class, I play ping-pong with my classmates.</speak>";

            var result = Task.WhenAll(
                this.getTtsAsync(text_of_frank_brown,"Brian",-100),
                this.getTtsAsync(text_of_gina_smith,"Donna",-100),
                this.getTtsAsync(text_of_wang_wei,"Andy",-100)).Result;

            List<byte> all = new List<byte>();

            foreach(var buffer in result)
            {
                all.AddRange(buffer);

            }

            File.WriteAllBytes("Result.mp3",all.ToArray());

        }

    }

    /// <summary>
    /// 听力测试
    /// </summary>

    public class HearingExam : Exam
    {
        public readonly string Name;

        public readonly int ExamCount;

        public readonly int CountPerExam;

        private List<string[]> questions = new List<string[]>();



        public HearingExam(string name, string[] files, int examCount, int countPerExam)
        {
            this.Name = name;
            this.ExamCount = examCount;
            this.CountPerExam = countPerExam;

            #region 整理题库


            foreach (var task_result in Task.WhenAll(files.Select(r => File.ReadAllLinesAsync(r))).Result)
            {
                foreach (var result in task_result)
                {
                    if (!result.Trim().StartsWith("#"))
                    {
                        result.Split('|').Run(r =>
                        {
                            if (r.Length > 1)
                            {
                                questions.Add(r.Take(2).ToArray());
                            }
                        });
                    }
                }
            }

            questions = questions.RandomSort().ToList();

            #endregion

        }


        //生成
        public async Task Gen(ELx lx) 
        {
            string path = Path.Combine("Output","Hearing", DateTime.Now.ToString("yyyyMMddHHmm"),lx.ToString());

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var tasks = new byte[ExamCount].Select((byte r,int i) =>
            {
                return GenExam(path,  i + 1, lx);
            });

            var answers = await Task.WhenAll(tasks);

            answers = answers.Select((string r ,int i)=>
            {
                return $"Exam {i+1}:{Environment.NewLine}{r}{Environment.NewLine}";

            }).ToArray();
            
            answers = new string[]{$"{path}{Environment.NewLine}"}.Concat(answers).ToArray();

            await File.WriteAllLinesAsync(Path.Combine(path,"answer.txt"), answers);

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="lx">类型 0 听英语写看汉语 1 听汉语写英语</param>
        /// <returns></returns>
        private async Task<string> GenExam(string path, int idx, ELx lx)
        {
            List<byte> audio = new List<byte>();

            //List<string[]> _questions = this.questions.RandomSort().Take(this.CountPerExam).ToList();

            int j = ((idx - 1) * 2 + (int)lx) * this.CountPerExam % this.questions.Count;

            var _questions = this.questions.Skip(j).Concat(this.questions.Take(j)).Take(this.CountPerExam).ToList();

            #region 导语

            string text = $"<speak bgm='http://nls.alicdn.com/bgm/2.wav' backgroundMusicVolumn='30' rate='-200' ><break time='2s' /><w>{this.Name}</w>同学，你好，现在是{lx}测试时间。请根据听到的题目写下内容，每题说两遍。<break time='1s' /></speak>";

            var buffer = await util.GetYyhcResultAsync(new AliyunUtil.YyhcReqest(text, "Aitong"));

            audio.AddRange(buffer);

            #endregion

            #region 问题

            var voices = new string[] { "donna", "luca" };

            if (lx == ELx.听汉语写英语)
            {
                voices = new string[] { "zhiyuan", "zhida" };
                _questions = _questions.Select(r=>r.Reverse().ToArray()).ToList();
            }

            StringBuilder answer = new StringBuilder();

            for (int i = 0; i < _questions.Count(); i++)
            {
                var question = _questions[i];

                text = $"<speak>{i + 1}<break time='1s' /></speak>";
                audio.AddRange(await getTtsAsync(text, "Luca"));

                text = $"<speak>{question[0]}<break time='2s' /></speak>";
                audio.AddRange(await getTtsAsync(text, voices[0]));

                text = $"<speak>{question[0]}<break time='10s' /></speak>";
                audio.AddRange(await getTtsAsync(text, voices[1]));

                answer.Append($"{i + 1}. {string.Join(" ", question)} ");

            }

            await File.WriteAllBytesAsync($"{path}/{idx}.mp3", audio.ToArray());

            return answer.ToString();

            #endregion


        }


        public enum ELx
        {
            听英语写汉语 = 1,
            听汉语写英语 = 2
        }
    }
}