export type Candidate01Speaker = 'narrator' | 'laiya' | 'roderick' | 'cain' | 'mirelle' | 'bran' | 'tasha' | 'ivra';

export interface StoryBeat {
  speaker: Candidate01Speaker;
  text: string;
}

export interface StoryPresentation {
  id: string;
  chapter: 1 | 2 | 3;
  kicker: string;
  title: string;
  date: string;
  location: string;
  scene: string;
  summary: string;
  beats: StoryBeat[];
  mission?: {
    objective: string;
    danger: string;
    lesson: string;
  };
}

export interface StoryChoiceOption {
  id: string;
  label: string;
  detail: string;
  consequence: string;
}

export interface StoryChoicePresentation {
  id: string;
  chapter: 1 | 2 | 3;
  prompt: string;
  context: string;
  options: StoryChoiceOption[];
}

export const CANDIDATE_01_SPEAKER_NAMES: Readonly<Record<Candidate01Speaker, string>> = {
  narrator: '断冠纪事', laiya: '莱娅', roderick: '罗德里克', cain: '凯恩', mirelle: '米蕾尔', bran: '布兰', tasha: '塔莎', ivra: '伊芙拉',
};

const SCENES = {
  hills: 'C01-CH01-S01',
  bridges: 'C01-CH01-S02',
  redstone: 'C01-ARCH-REDSTONE',
  burned: 'C01-CH01-S05',
  river: 'C01-CH02-S06',
  council: 'C01-CH05-S26',
  rations: 'C01-SCENE-PUBLIC-RATIONS',
  ivra: 'C01-CH02-S09',
  forge: 'C01-CH03-S14',
  silverwood: 'C01-ARCH-SILVERWOOD',
  valley: 'C01-ARCH-THREE-BRIDGES',
} as const;

const p = (
  id: string,
  chapter: 1 | 2 | 3,
  kicker: string,
  title: string,
  date: string,
  location: string,
  scene: string,
  summary: string,
  beats: StoryBeat[],
  mission?: StoryPresentation['mission'],
): StoryPresentation => ({ id, chapter, kicker, title, date, location, scene, summary, beats, mission });

const STORY: StoryPresentation[] = [
  p('c01/prologue', 1, '序章', '一双没有补好的靴子', '断冠纪第 318 年 · 初夏', '灰境边防营地', SCENES.hills,
    '在战争成为地图上的箭头以前，它先是一双漏水的靴子。十八岁的莱娅第一次得到独立指挥权。', [
      { speaker: 'narrator', text: '罗德里克第三次拆开她的护腕时，天还没有亮。湿木在火心里嘶响，托伦裂开的鞋底正往外挤泥。' },
      { speaker: 'roderick', text: '旗官先看撤路，再看高地。士兵不会因为你的答案正确，就少走一里泥。' },
      { speaker: 'laiya', text: '我会把他们都带回来。' },
      { speaker: 'roderick', text: '别许一个你还不知道怎样兑现的誓。先记住他们的名字。' },
    ]),

  p('c01/brief-01', 1, '第一章 · 战斗 01', '双子丘陵', '第 0 年 · 初夏', '灰境北丘', SCENES.hills,
    '莱娅的第一次独立指挥。高地能赢下一场遭遇战，也能让一支脱离援护的小队死得更快。', [
      { speaker: 'roderick', text: '北丘看得见整条大道。也就是说，整条大道都看得见你。' },
      { speaker: 'laiya', text: '帝国前锋还没站稳。现在上去，我们能先拿村庄。' },
      { speaker: 'narrator', text: '两份互称对方先越境的军报，在同一个清晨把年轻人推向彼此。' },
    ], { objective: '莱娅存活并击退维尔萨前锋。', danger: '抢占北丘会让托伦提前进入包围；罗德里克只会救场一次。', lesson: '高低差、朝向、援护与确定性伤害预测。' }),
  p('c01/aftermath-01', 1, '战后', '相同日期的命令', '第 0 年 · 初夏', '双子丘陵中央村', SCENES.hills,
    '胜利没有解释战争。帝国尸体上的调令与洛恩军令在同一天签发。', [
      { speaker: 'laiya', text: '如果他们也收到“洛恩先越境”的命令呢？' },
      { speaker: 'roderick', text: '怀疑可以让你再看一遍证据。不能让它替你拖延下一次决定。' },
    ]),

  p('c01/brief-02', 1, '第一章 · 战斗 02', '三桥河谷', '第 0 年 · 盛夏', '白河三桥', SCENES.bridges,
    '三座桥把两国军队挤进同一条射线；难民又把胜负从桥面上挤开。', [
      { speaker: 'cain', text: '中桥上的弓手停火。平民通过以前，任何一次放箭都记在我的名下。' },
      { speaker: 'laiya', text: '你凭什么相信我也会停？' },
      { speaker: 'cain', text: '我不相信你。我只是先让我的人停下，然后承担判断错你的后果。' },
    ], { objective: '护送三名平民抵达东岸，并摧毁无标记弩车。', danger: '前三回合中桥处于停火区；远处弩车不受停火约束。', lesson: '护送、狭窄桥面、临时交战规则与最小射程。' }),
  p('c01/aftermath-02', 1, '战后', '没有道谢的停火', '第 0 年 · 盛夏', '白河东岸', SCENES.bridges,
    '双方各守一岸，谁也没有得到可以写进军报的胜利。一个受伤孩子被帝国盾卫交到洛恩牧师手里。', [
      { speaker: 'cain', text: '弩车没有番号。查出它从哪里来，再决定下次见面时该把枪指向谁。' },
      { speaker: 'laiya', text: '你可以留下来一起查。' },
      { speaker: 'cain', text: '我的职责还没有允许我相信你。' },
    ]),

  p('c01/brief-03', 1, '第一章 · 战斗 03', '赤石围城', '第 0 年 · 长夏', '赤石堡垒', SCENES.redstone,
    '城上挂着帝国旗，城内却是失踪的洛恩士兵。夺城和救人第一次成为两个不同目标。', [
      { speaker: 'mirelle', text: '守军在重复同一句誓文。他们还活着，只是名字被塔压在命令下面。' },
      { speaker: 'roderick', text: '我不会阻止你关闭节点。但十四回合以后，援军会把所有复杂都当成敌人处理。' },
      { speaker: 'laiya', text: '那就在他们抵达以前，把复杂的人带回来。' },
    ], { objective: '占领赤石城堡，或深入后方关闭控制节点。', danger: '正门有重弩和重甲；绕行更安全但会消耗回合。', lesson: '攻城、复合路线、魔法克甲与非致命胜利。' }),
  p('c01/aftermath-03', 1, '战后', '名字归于谁', '第 0 年 · 长夏', '赤石塔下', SCENES.redstone,
    '控制解除后，守军只记得自己奉洛恩之命进塔。后来覆盖上去的帝国旗，成了最便宜的证词。', [
      { speaker: 'mirelle', text: '教廷在收集所有阵亡者的名字。不是为了安葬，是为了让命令可以继续使用他们。' },
      { speaker: 'roderick', text: '证据封存，送往王都。莱娅，你仍然要服从眼前愿意为后果负责的人。' },
    ]),

  p('c01/brief-04', 1, '第一章 · 战斗 04', '黑旗营地', '第 0 年 · 初秋', '灰境西林', SCENES.council,
    '只侦察、不交战的命令，在第一名即将被处决的俘虏面前失去完整意义。', [
      { speaker: 'mirelle', text: '他们在反复抽取士兵最后一次听令的记忆。每抽一次，那个人就少一点自己。' },
      { speaker: 'laiya', text: '三处营帐。证人和档案都带走。' },
      { speaker: 'narrator', text: '她第一次主动把“我负责”写在一份明确违背导师命令的计划下面。' },
    ], { objective: '调查三处营帐，再让莱娅与米蕾尔从西侧撤离。', danger: '调查会拉长暴露时间；撤离前不要把全队压进营地。', lesson: '阶段目标、战场变量、调查触发与撤退路线。' }),
  p('c01/aftermath-04', 1, '战后', '违令留下的证人', '第 0 年 · 初秋', '黑旗营外', SCENES.council,
    '获救者记得同一份誓文模板被分别写进洛恩与维尔萨命令。证据足以让人被灭口，还不足以让国家停战。', [
      { speaker: 'mirelle', text: '档案可以复制，证人不能。今晚先记他们的名字。' },
      { speaker: 'laiya', text: '也记下是我让他们继续冒险。不能只在成功以后才承认那道命令是我的。' },
    ]),

  p('c01/brief-05', 1, '第一章 · 战斗 05', '焚村令', '第 0 年 · 深秋', '苇草村', SCENES.burned,
    '王国命令焚毁“受污染”的村庄。污染是伪造的，执行命令的人却是真的。', [
      { speaker: 'roderick', text: '让开，莱娅。你还能回到队列里。' },
      { speaker: 'laiya', text: '如果回去的条件是把他们留在火里，那条队列已经不是我的了。' },
      { speaker: 'bran', text: '旗子没有颜色时最诚实。今天谁站在村口，明天我就记得谁。' },
    ], { objective: '至少保护两名村民并坚守七回合。', danger: '净化军第四回合增援；不要让脆弱单位堵死治疗线。', lesson: '生存目标、关键单位保护、增援与军团士气。' }),
  p('c01/aftermath-05', 1, '第一章终', '灰旗升起', '第 0 年 · 深秋', '烧毁的苇草村', SCENES.burned,
    '她失去军衔、补给和合法身份。烧去纹章的旗布在晨风里只剩灰色，却第一次允许人自行决定是否跟随。', [
      { speaker: 'bran', text: '我不向你宣誓。我跟你走，直到你也开始替别人决定他们该烧在哪里。' },
      { speaker: 'laiya', text: '那时你要先告诉我，然后离开。不要等我批准。' },
    ]),

  p('c01/brief-06', 2, '第二章 · 战斗 06', '白河夜渡', '第 1 年 · 深冬', '白河南岸', SCENES.river,
    '抗命只花了一夜，维持一支带着伤员和孩子的队伍却要花掉每一顿饭。', [
      { speaker: 'bran', text: '芦苇浅滩能走人，不能过马和粮车。别说全都要，河水不听演说。' },
      { speaker: 'mirelle', text: '伤员能再撑八回合。第九回合开始，寒冷会替追兵做决定。' },
      { speaker: 'laiya', text: '先把每一条路线写清楚，再说我们愿意失去什么。' },
    ], { objective: '八回合内护送四名非战斗人员抵达北岸。', danger: '护送单位移动慢，追兵骑兵会从侧翼切入。', lesson: '多单位护送、时间压力、后卫与资源取舍。' }),
  p('c01/aftermath-06', 2, '战后', '渡河以后仍会死人', '第 1 年 · 深冬', '白河北岸', SCENES.river,
    '清点人数时，冻伤和饥饿没有因为任务完成而消失。莱娅把口粮递给伤员，被布兰推了回来。', [
      { speaker: 'bran', text: '饿死领袖只会让明天多一个要埋的人。组织不是看你肯死几次。' },
      { speaker: 'laiya', text: '那就从今天起，口粮先按名单发，不按谁更会拒绝。' },
    ]),

  p('c01/brief-07', 2, '第二章 · 战斗 07', '无主之城', '第 1 年 · 初春', '自由城洛岬', SCENES.rations,
    '领主逃走后，粮仓、城门和兵营分别属于三群无法互相信任的人。敌军不会等他们开完会议。', [
      { speaker: 'laiya', text: '我们守城，但不接管你们的钥匙。哪两处先修，由城里的人当面说。' },
      { speaker: 'bran', text: '当面说会吵。' },
      { speaker: 'laiya', text: '那就让争吵发生在敌人进城以前，而不是命令发生在他们头上。' },
    ], { objective: '占领联合粮仓与民兵营。', danger: '荒原骑手和王国征讨军从不同方向逼近。', lesson: '多方向接敌、城市走廊、占领与防线取舍。' }),
  p('c01/aftermath-07', 2, '战后', '谁拥有城墙', '第 1 年 · 初春', '洛岬议事厅', SCENES.rations,
    '城守住了，钥匙仍有三串。莱娅第一次发现，胜利以后留下来处理粮价比攻下一座城更难。', [
      { speaker: 'laiya', text: '灰旗军可以提供守卫，不提供永远正确的市长。' },
      { speaker: 'narrator', text: '商人、民兵与难民代表第一次坐在同一张桌边，也第一次同时对她不满。' },
    ]),

  p('c01/brief-08', 2, '第二章 · 战斗 08', '佣兵之价', '第 1 年 · 暮春', '洛岬粮道', SCENES.rations,
    '佣兵扣住粮道索取欠饷，商人把死人从账册上删掉。真正的盗匪在争执最响时来了。', [
      { speaker: 'tasha', text: '你们这些有旗的人，总喜欢用明天的世界支付今天的死人。' },
      { speaker: 'laiya', text: '名字、数目、日期。先把欠下的写清楚。然后一起把粮仓守住。' },
      { speaker: 'tasha', text: '这句话值不了一枚铜币。但我愿意看你把它写进制度。' },
    ], { objective: '保护塔莎并击退抢粮者。', danger: '骑手会绕过正面；塔莎是高价值的爆破单位，也是失败条件。', lesson: '关键同盟保护、侧翼机动与范围爆破。' }),
  p('c01/aftermath-08', 2, '战后', '一份可以解除的契约', '第 1 年 · 暮春', '洛岬粮仓', SCENES.council,
    '塔莎没有宣誓效忠。她签下一份列明期限、欠款与退出条款的契约，并把第一份抚恤名单钉在军需处。', [
      { speaker: 'tasha', text: '我不是因为相信你才加入。我是因为这份纸允许我在不再相信你时离开。' },
      { speaker: 'laiya', text: '那它比誓言更有用。' },
    ]),

  p('c01/brief-09', 2, '第二章 · 战斗 09', '笼中之火', '第 1 年 · 盛夏', '洛岬北部大道', SCENES.ivra,
    '教廷运输队带着粮食、药品和一只被符文锁链束缚的幼龙。控制它是最快的解法，也是最危险的先例。', [
      { speaker: 'mirelle', text: '我可以念出绑定誓文。它会替我们作战——也会知道是我把新锁套在旧锁外面。' },
      { speaker: 'tasha', text: '四个节点，拆掉三个就会失稳。慢一点，但选择仍然属于笼子里的那一方。' },
      { speaker: 'laiya', text: '拆节点。它出来以后愿不愿意帮忙，不算在计划里。' },
    ], { objective: '破坏四处锁链节点中的三处。', danger: '节点分散且有护卫；爆破火药的冷却需要预先规划。', lesson: '复合结构、分兵、攻城加成与非控制解法。' }),
  p('c01/aftermath-09', 2, '战后', '它没有加入', '第 1 年 · 盛夏', '破碎的囚笼旁', SCENES.ivra,
    '幼龙挣脱后先咬断最后一段锁链，然后飞到够不着任何命令的岩脊上。', [
      { speaker: 'ivra', text: '你们没有命令我。' },
      { speaker: 'laiya', text: '也不会因为这件事要求你留下。' },
      { speaker: 'narrator', text: '伊芙拉没有回答。第二天队伍出发时，云上的影子与他们保持了整整半日距离。' },
    ]),

  p('c01/brief-10', 2, '第二章 · 战斗 10', '旧旗下的追兵', '第 1 年 · 初秋', '灰境东部山口', SCENES.council,
    '罗德里克带着真正的赦免令追来。回去可以活，留下才要继续承担灰旗造成的后果。', [
      { speaker: 'roderick', text: '我没有伪造这份赦免。放下武器的人可以回家。你也可以。' },
      { speaker: 'laiya', text: '回去以后，下一道焚村令由谁拒绝？' },
      { speaker: 'roderick', text: '你把一次正确的抗命，误当成了永远正确的资格。' },
    ], { objective: '让莱娅抵达东部山口，不必歼灭旧部。', danger: '罗德里克的骑兵和援护阵线正面强度极高。', lesson: '突破、士气、飞行侧翼与有限交战。' }),
  p('c01/aftermath-10', 2, '第二章终', '允许退出的誓', '第 1 年 · 初秋', '山口外营地', SCENES.council,
    '追兵没有被消灭，灰旗也没有被带回去。每一名留下的人重新听到一遍退出条款。', [
      { speaker: 'laiya', text: '今天留下，不等于明天必须留下。我们服从共同写下的规则，不服从我这个人。' },
      { speaker: 'tasha', text: '很好。现在把“共同写下”需要几个人签字也写清楚。' },
    ]),

  p('c01/brief-11', 3, '第三章 · 战斗 11', '无声墓园', '第 2 年 · 早春', '圣辉领外墓园', SCENES.redstone,
    '同一名士兵在同一座墓前反复死亡。净化会让回声消散，读出真名才可能让他停止听令。', [
      { speaker: 'mirelle', text: '别先攻击巨像。墓碑上有三段被刮掉的姓名，先让它们重新被听见。' },
      { speaker: 'bran', text: '我认识其中一个。他在苇草村借过盐，没还。活人的记忆也算证据。' },
      { speaker: 'laiya', text: '分三路读碑。等米蕾尔说可以，再让巨像安息。' },
    ], { objective: '读取三处真名，再击败墓园巨像。', danger: '亡灵会拖住路线；米蕾尔的断誓攻击有明显克制优势。', lesson: '阶段目标、题材克制、巨型单位与亡者标记。' }),
  p('c01/aftermath-11', 3, '战后', '墓碑拒绝净化', '第 2 年 · 早春', '无声墓园', SCENES.redstone,
    '最后一个名字被读出时，石像没有爆炸，只是像终于听见休息命令的人一样跪了下去。', [
      { speaker: 'mirelle', text: '安息不该是活人替死者按下的删除键。' },
      { speaker: 'bran', text: '那就保留欠盐这件事。完整的人不只剩英雄事迹。' },
    ]),

  p('c01/brief-12', 3, '第三章 · 战斗 12', '沉没钟塔', '第 2 年 · 暮春', '沉钟河谷', SCENES.valley,
    '钟塔机构失控，河水正在上涨。凯恩带着帝国军也来关闭它；王冠碎片只会留给一方。', [
      { speaker: 'cain', text: '两处机构同时关闭，河谷才能保住。碎片归属可以在不淹死村庄以后争。' },
      { speaker: 'laiya', text: '共同控制意味着你的士兵也能拒绝我的操作。' },
      { speaker: 'cain', text: '也意味着你的人能拒绝我。这正是共同二字不方便的地方。' },
    ], { objective: '瘫痪三处钟塔机构中的两处。', danger: '水路切断阵线，机构本身具有高耐久。', lesson: '复合设施、狭道、临时盟友与攻城分工。' }),
  p('c01/aftermath-12', 3, '战后', '共同控制的半小时', '第 2 年 · 暮春', '沉钟塔顶', SCENES.valley,
    '双方的手同时放在不同控制杆上。谁都不能单独启动钟塔，这是他们第一次真正共同拥有一项权力。', [
      { speaker: 'cain', text: '这套程序效率很低。' },
      { speaker: 'laiya', text: '但你还站在这里，我也没有被你命令离开。低一点可以接受。' },
    ]),

  p('c01/brief-13', 3, '第三章 · 战斗 13', '圣城档案', '第 2 年 · 盛夏', '圣辉白塔', SCENES.council,
    '米蕾尔回到曾教她删除修改痕迹的地方。三组档案分别保存战争、命令与她老师的签名。', [
      { speaker: 'mirelle', text: '档案室会在第三份卷宗离架时报警。不是因为内容重要，是因为删改记录还在。' },
      { speaker: 'tasha', text: '那就先规划出口。证据没长腿，拿证据的人有。' },
      { speaker: 'laiya', text: '读三处，米蕾尔带原件走。其余人只负责让她还能走。' },
    ], { objective: '读取三组档案，并护送米蕾尔到东侧出口。', danger: '阶段完成后仍需穿过警戒部队，不要过早消耗支援能力。', lesson: '连续阶段、关键携带者、城市掩体与撤离。' }),
  p('c01/aftermath-13', 3, '战后', '保留修改痕迹', '第 2 年 · 盛夏', '圣城外旧医院', SCENES.council,
    '档案证明两国军令使用同一套誓文源。米蕾尔没有抹掉老师的名字，也没有替她写上原谅。', [
      { speaker: 'mirelle', text: '如果我删除她的签名，我就用同一种方法证明她错。' },
      { speaker: 'laiya', text: '那就让名字、罪责和修改都留在同一页上。' },
    ]),

  p('c01/brief-14', 3, '第三章 · 战斗 14', '山炉余烬', '第 2 年 · 深秋', '山炉下层炉城', SCENES.forge,
    '氏族内战唤醒石魔像，熔流阀开始失控。修炉心必须使用制造王冠的同一套技术。', [
      { speaker: 'tasha', text: '技术不会替祖先认罪，也不会因为祖先有罪就自动失效。两处阀门，先关再谈。' },
      { speaker: 'bran', text: '谈的时候别让“工具无罪”变成没人负责。' },
      { speaker: 'laiya', text: '完整保存维修记录。我们用过什么，就留下什么。' },
    ], { objective: '关闭两处熔流阀，并平息失控守卫。', danger: '熔流不可通行，石魔像会强制位移造成碰撞伤害。', lesson: '危险地貌、强制位移、重甲破防与工程路线。' }),
  p('c01/aftermath-14', 3, '战后', '修好并不等于无罪', '第 2 年 · 深秋', '山炉记录厅', SCENES.forge,
    '炉心恢复，族长要求封存王冠图纸。塔莎把维修页和责任页订在同一个册子里。', [
      { speaker: 'tasha', text: '不公开全部细节，可以讨论。假装我们没用过它，不可以。' },
      { speaker: 'laiya', text: '技术可以限制访问，责任不能限制见证。' },
    ]),

  p('c01/brief-15', 3, '第三章 · 战斗 15', '银林长梦', '第 3 年 · 初春', '银林母根', SCENES.silverwood,
    '银林把森林衰亡转移给林外城市已有百年。停止转移会伤害母根，继续则会让陌生人替它死亡。', [
      { speaker: 'bran', text: '他们说代价落在看不见的地方，所以不是他们的决定。' },
      { speaker: 'laiya', text: '保护母根，但先切断继续扩大的誓文。活下来的森林要自己承担以后。' },
      { speaker: 'ivra', text: '不能把疼痛命名为远方。' },
    ], { objective: '保护母根，击退维持代价转移的受控部队。', danger: '长弓射程远且克制伊芙拉；母根一旦被毁立即失败。', lesson: '森林视线、远程压制、对空克制与结构保护。' }),
  p('c01/aftermath-15', 3, '战后', '森林第一次自己疼', '第 3 年 · 初春', '银林树城', SCENES.silverwood,
    '转移减弱后，母根的叶片在一夜间落去三分之一。银林没有感谢灰旗，只同意让林外城市进入下一次决策。', [
      { speaker: 'laiya', text: '共同承担不会让代价变小，只会让被牺牲的人不再缺席。' },
      { speaker: 'bran', text: '他们还恨你。很好，至少这次恨你的人坐在桌边。' },
    ]),

  p('c01/brief-16', 3, '第三章 · 战斗 16', '记住我的名字', '第 3 年 · 暮春', '旧帝国归名场', SCENES.redstone,
    '无数无名死者聚成“无旗者”。审判官要继续抽取它，莱娅必须先证明新意识有权拒绝被使用。', [
      { speaker: 'mirelle', text: '它不是任何一个死者复活。三个真名只是让它知道，组成自己的记忆曾经属于谁。' },
      { speaker: 'laiya', text: '那就回应三个名字，击退审判官。不要把无旗者当成要被击杀的目标。' },
      { speaker: 'ivra', text: '名字不是锁。名字是可以回答，也可以不回答的声音。' },
    ], { objective: '回应三个真名，并击退抽取记忆的审判官。', danger: '墓园巨像仍会阻挡道路；任务不要求消灭它。', lesson: '非歼灭终局、战场互动、目标优先级与火力克制。' }),
  p('c01/aftermath-16', 3, '第三章终', '一个新名字', '第 3 年 · 暮春', '归名场', SCENES.redstone,
    '审判官撤退后，聚忆体没有散去。它第一次用不是任何一名死者的声音，为自己选择了一个称呼。', [
      { speaker: 'mirelle', text: '我们记下它过去由谁组成，也记下它现在不等于其中任何一个人。' },
      { speaker: 'laiya', text: '如果我们要建立一个允许退出的盟约，它也拥有同样的退出权。' },
    ]),

  p('c01/chapter-three-ending', 3, '前三章完成', '灰旗成为一支军队', '第 3 年 · 初夏', '洛岬晨议会', SCENES.council,
    '三年以前，莱娅相信正确的人下令就足够。现在她拥有一支军队，也第一次拥有能在她错误时阻止她的人。', [
      { speaker: 'tasha', text: '十六场战斗，七份退出条款，三本公开账册。你终于开始变得没那么不可替代。' },
      { speaker: 'cain', text: '王冠碎片正在醒来。下一次共同控制，不会只持续半小时。' },
      { speaker: 'laiya', text: '那就先让每一个会被它命令的人拥有席位。第四章从这张桌子开始。' },
      { speaker: 'narrator', text: '灰旗在晨风里没有王徽。桌边每个人仍能看见门，也仍然拥有走出去的权利。' },
    ]),
  p('c01/campaign-failed', 1, '战役中断', '灰旗倒下', '未定', '伊瑟兰', SCENES.burned,
    '这条时间线在这里中断。失败可以重新推演，但不会被叙事假装成没有发生。', [
      { speaker: 'narrator', text: '重新开始当前战斗，或从战役存档继续。每一次选择仍会保留它对应的代价。' },
    ]),
];

const stories = new Map(STORY.map((entry) => [entry.id, entry]));

const CHOICE_STORY: StoryChoicePresentation[] = [
  { id: 'c01/choice-first-command', chapter: 1, prompt: '莱娅如何开始第一次指挥？', context: '北丘提供高地优势，但先遣小队会脱离罗德里克的援护。', options: [
    { id: 'rush-north-hill', label: '抢占北丘', detail: '让托伦提前进入高地，取得位置优势但以受伤状态开局。', consequence: '提高“果断”，并真实承担救援成本。' },
    { id: 'steady-advance', label: '稳步推进', detail: '保持队形与援护，让帝国前锋先靠近中央村。', consequence: '提高“克制”，保留完整阵线。' },
  ] },
  { id: 'c01/choice-bridge-truce', chapter: 1, prompt: '是否接受凯恩提出的中桥停火？', context: '正式承诺会约束双方三回合，也会让凯恩把你的选择记在程序里。', options: [
    { id: 'accept-truce', label: '接受停火', detail: '中桥三回合禁止攻击，优先让平民通过。', consequence: '凯恩关系 +1。' },
    { id: 'no-formal-promise', label: '不作承诺', detail: '仍可实际停火，但战场不会强制限制攻击。', consequence: '敌方更积极，增加独立倾向。' },
  ] },
  { id: 'c01/choice-black-camp-plan', chapter: 1, prompt: '黑旗营地同时有俘虏和档案，如何分配兵力？', context: '两种方案都违背“只侦察”的命令，差别在于把主要风险放在哪里。', options: [
    { id: 'rescue-and-evidence', label: '救人为先', detail: '主力护送证人，米蕾尔沿途保存关键模板。', consequence: '证人数增加。' },
    { id: 'split-the-force', label: '分兵取证', detail: '同时搜索三处营帐，路线更长、信息更完整。', consequence: '证据值增加。' },
  ] },
  { id: 'c01/choice-white-river-priority', chapter: 2, prompt: '白河撤离优先保住什么？', context: '河道不允许所有人、重装备和粮食同时安全通过。', options: [
    { id: 'people-first', label: '人员优先', detail: '把可用护卫全部配给难民与伤员。', consequence: '难民关系 +2。' },
    { id: 'save-supplies', label: '物资优先', detail: '保住决定后续恢复能力的粮车。', consequence: '战役补给 +3。' },
  ] },
  { id: 'c01/choice-mercenary-contract', chapter: 2, prompt: '如何承认佣兵欠款？', context: '现金能解决今天，制度决定下一个死者会不会再次被从账上删除。', options: [
    { id: 'pay-what-is-owed', label: '立即补发欠款', detail: '动用有限国库，按姓名补齐现有欠款。', consequence: '国库 -2，塔莎关系 +2。' },
    { id: 'create-pension-ledger', label: '建立抚恤账册', detail: '先确立不可删除的长期债权，再分期支付。', consequence: '制度能力 +1，塔莎关系 +1。' },
  ] },
  { id: 'c01/choice-free-ivra', chapter: 2, prompt: '用什么方式解除伊芙拉的锁链？', context: '两种方式都不控制幼龙，只决定战斗中的工具与路径。', options: [
    { id: 'mirelle-ritual', label: '断誓仪式', detail: '米蕾尔削弱节点，让每处锁链更容易摧毁。', consequence: '米蕾尔关系 +1。' },
    { id: 'break-chain-nodes', label: '工兵拆解', detail: '依靠塔莎和工兵逐处爆破，保留节点结构证据。', consequence: '塔莎关系 +1。' },
  ] },
  { id: 'c01/choice-old-banner-terms', chapter: 2, prompt: '怎样对待仍站在旧旗之下的士兵？', context: '他们执行追捕命令，但其中许多人真心相信赦免能让同伴回家。', options: [
    { id: 'offer-exit-right', label: '允许自行离队', detail: '公开承诺投降或脱离追兵者不会被追责。', consequence: '制度能力 +1。' },
    { id: 'demand-surrender', label: '必须放下武器', detail: '只有正式投降才能获得保护。', consequence: '权威倾向 +1。' },
  ] },
  { id: 'c01/choice-bell-tower-control', chapter: 3, prompt: '沉钟塔由谁控制？', context: '共同控制效率低，独占碎片则会重演双方互不信任的竞赛。', options: [
    { id: 'joint-control-with-cain', label: '与凯恩共管', detail: '凯恩及其军团作为临时友军进入战场。', consequence: '凯恩关系 +2。' },
    { id: 'race-for-the-fragment', label: '争夺碎片', detail: '各自关闭机构，先完成者带走碎片。', consequence: '王冠碎片 +1。' },
  ] },
  { id: 'c01/choice-forge-record', chapter: 3, prompt: '如何处理山炉的王冠技术记录？', context: '完全公开会扩散危险，完全封存会再次切断公共追责。', options: [
    { id: 'publish-the-record', label: '公开责任记录', detail: '隐藏可复现细节，但公开参与者、用途和代价。', consequence: '制度能力 +1，山炉关系 -1。' },
    { id: 'seal-dangerous-pages', label: '暂时封存图纸', detail: '由多方保管完整记录，暂不向公众展示。', consequence: '山炉关系 +1。' },
  ] },
  { id: 'c01/choice-silverwood-price', chapter: 3, prompt: '怎样结束银林的代价转移？', context: '立即停止会伤害母根，逐步退出仍会让林外城市继续承担一段时间。', options: [
    { id: 'end-cost-transfer', label: '立即停止转移', detail: '让森林从本场战斗开始承担全部真实损耗。', consequence: '银林关系 -1。' },
    { id: 'phase-out-gradually', label: '限期逐步退出', detail: '设定公开期限和林外城市席位，降低断裂冲击。', consequence: '银林关系 +1，制度能力 +1。' },
  ] },
  { id: 'c01/choice-unflagged-personhood', chapter: 3, prompt: '灰旗如何称呼“无旗者”？', context: '它由许多亡者记忆组成，却已经做出不属于任何单个亡者的新选择。', options: [
    { id: 'recognize-a-new-person', label: '承认新人格', detail: '正式承认它拥有姓名、拒绝与离开的权利。', consequence: '归名者关系 +2。' },
    { id: 'protect-until-heard', label: '先保护其表达', detail: '不急于替它定义人格，在外部威胁中保护它继续回答。', consequence: '归名者关系 +1，克制 +1。' },
  ] },
];

const choices = new Map(CHOICE_STORY.map((entry) => [entry.id, entry]));

export function candidate01Story(id: string): StoryPresentation {
  const story = stories.get(id);
  if (!story) throw new Error(`unknown candidate-01 presentation "${id}"`);
  return story;
}

export function candidate01Choice(id: string): StoryChoicePresentation {
  const choice = choices.get(id);
  if (!choice) throw new Error(`unknown candidate-01 choice presentation "${id}"`);
  return choice;
}
