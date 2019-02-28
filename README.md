# tesseract-train
基于tesseract的训练工具<br>

* 采用VS2013编译生成tesseract-train.exe文件，把该文件放到需要识别的图片(.tif)目录中启动<br>
* 在界面上配置自动识别工具路径[tesseract path]，并指定语言[lang]和字体[font]<br>
* 训练图片路径[train image path]默认为当前程序运行目录<br>
* 程序启动后会生成三个临时目录<br>
[TempBox]   合并图片后保存的文件目录文件名称：{lang}.{font}.exp0.tif<br>
[TempImg]   对手工识别后保存的图片目录<br>
[TempTrain] 对标识后图片进行训练生成的中间文件目录，最终生成{lang}.traineddata文件<br>
* 程序运行后，界面上有三个按钮<br>
[makebox]根据图片路径合并文件并按行方式识别，识别结果显示在界面上，中间生成文件存放在[TempBox]中<br>
[save mark]如果界面上识别正确的字符，可以双击文本框，进行人工标记，双击后图标保存到[TempImg]目录中，点击本按钮后生成mark.txt文件<br>
[train]点击该按钮后，根据mark.txt和[TempImg]目录生成训练文件保存到[TempTrain]并把训练文件复制到[tesseract path]\tessdata目录下<br>
* 每次makebox时，都会判断是否已存在设定语言的训练包，这样可以每次训练时，准确率有所提高<br>
* 例如：lang=ts  font=yzm   相关的命令行如下<br>
tesseract ts.yzm.exp0.tif ts.yzm.exp0 -l ts -psm 10 batch.nochop makebox<br>
<br>
tesseract ts.yzm.exp0.tif ts.yzm.exp0 -l ts -psm 10 nobatch box.train<br>
unicharset_extractor ts.yzm.exp0.box<br>
echo ts 0 0 0 0 0 >> font_properties<br>
shapeclustering -F font_properties -U unicharset ts.yzm.exp0.tr<br>
mftraining -F font_properties -U unicharset -O ts.unicharset ts.yzm.exp0.tr<br>
cntraining ts.yzm.exp0.tr<br>
<br>
rename normproto ts.normproto<br>
rename inttemp ts.inttemp<br>
rename pffmtable ts.pffmtable<br>
rename shapetable ts.shapetable<br>
<br>
combine_tessdata ts<br>
<br>
copy ts.traineddata E:\tesseract-ocr\tessdata /y<br>
